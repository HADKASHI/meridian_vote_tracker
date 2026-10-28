using System.Diagnostics;
using Meridian.Data;
using Meridian.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Meridian.Platform.Jobs;

/// <summary>
/// Runs jobs from the <c>job</c> table, which is the queue. Claims take an advisory lock,
/// so the limit on running imports holds across any number of workers; a running job
/// heartbeats, a silent one is requeued by the sweeper, and every update is conditional
/// on the attempt, so a worker that was swept can no longer touch the job. The
/// BackgroundService drives it every poll; tests call it on demand.
/// </summary>
public sealed class JobProcessor(
    NpgsqlDataSource dataSource,
    IServiceScopeFactory scopes,
    IOptions<JobOptions> options,
    ILogger<JobProcessor> logger)
{
    public const string ActivitySourceName = "Meridian.Jobs";
    private static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>Serialises claims, so "running imports under the limit" is counted exactly.</summary>
    private const long ClaimLockKey = 0x4D_45_52_49_44_49_41_4E; // "MERIDIAN"

    private JobOptions Options => options.Value;

    /// <summary>Runs queued jobs one after another until none can be claimed. Returns how many ran.</summary>
    public async Task<int> RunUntilIdleAsync(CancellationToken cancellationToken)
    {
        var ran = 0;
        while (await ClaimNextAsync(cancellationToken) is { } job)
        {
            await RunClaimedAsync(job, cancellationToken);
            ran++;
        }
        return ran;
    }

    /// <summary>
    /// Claims the oldest queued job that may start now: an import while fewer than
    /// <c>Imports:MaxConcurrent</c> run, a snapshot while no other snapshot runs.
    /// </summary>
    public async Task<ClaimedJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("key", ClaimLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var claim = new NpgsqlCommand("""
            UPDATE job
            SET state = 'running', attempts = attempts + 1, heartbeat_at = now(),
                started_at = coalesce(started_at, now())
            WHERE id = (
              SELECT id FROM job
              WHERE state = 'queued' AND (
                (kind = 'import'
                  AND (SELECT count(*) FROM job WHERE state = 'running' AND kind = 'import') < @maxImports)
                OR (kind = 'snapshot'
                  AND NOT EXISTS (SELECT 1 FROM job WHERE state = 'running' AND kind = 'snapshot')))
              ORDER BY id
              LIMIT 1
              FOR UPDATE SKIP LOCKED)
            RETURNING id, kind, import_id, snapshot_id, attempts, trace_parent
            """, connection, transaction);
        claim.Parameters.AddWithValue("maxImports", (long)Options.MaxConcurrentImports);

        ClaimedJob? job = null;
        await using (var reader = await claim.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                job = new ClaimedJob(
                    reader.GetInt64(0),
                    Enum.Parse<JobKind>(reader.GetString(1), ignoreCase: true),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5));
        }
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    /// <summary>
    /// Requeues running jobs with no heartbeat for <c>Jobs:StaleAfter</c>, or fails them once
    /// their attempts are used up. Returns how many it touched.
    /// </summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE job
            SET state = CASE WHEN attempts >= @maxAttempts THEN 'failed' ELSE 'queued' END,
                finished_at = CASE WHEN attempts >= @maxAttempts THEN now() END,
                heartbeat_at = NULL,
                last_error = @error
            WHERE state = 'running' AND heartbeat_at < now() - @staleAfter
            RETURNING id, state, attempts
            """);
        command.Parameters.AddWithValue("maxAttempts", Options.MaxAttempts);
        command.Parameters.AddWithValue("staleAfter", Options.StaleAfter);
        command.Parameters.AddWithValue("error",
            $"No heartbeat for {Options.StaleAfter.TotalSeconds:0.#} s: the worker stopped or stalled.");

        var swept = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            swept++;
            logger.LogWarning("Job {JobId} had no heartbeat after attempt {Attempt}; now {State}",
                reader.GetInt64(0), reader.GetInt32(2), reader.GetString(1));
        }
        return swept;
    }

    /// <summary>Runs a claimed job to done, a retry, or failed. Never throws.</summary>
    public async Task RunClaimedAsync(ClaimedJob job, CancellationToken stoppingToken)
    {
        using var activity = StartActivity(job);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = job.Id,
            [job.Kind == JobKind.Import ? "ImportId" : "SnapshotId"] = job.ImportId ?? job.SnapshotId,
        });
        await using var services = scopes.CreateAsyncScope();
        var handler = services.ServiceProvider.GetServices<IJobHandler>().FirstOrDefault(h => h.Kind == job.Kind);
        if (handler is null)
        {
            var kind = SnakeCaseEnumConverter<JobKind>.ToName(job.Kind);
            logger.LogError("No handler is registered for {Kind} jobs", kind);
            await FinishAsync(job, JobState.Failed, $"No handler is registered for {kind} jobs.", CancellationToken.None);
            return;
        }

        var limit = Options.TimeoutFor(job.Kind);
        using var timeout = new CancellationTokenSource(limit);
        using var running = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatAsync(job, heartbeatStop.Token);
        var context = new JobContext(job, (rows, done, total, ct) => ReportAsync(job, rows, done, total, ct));

        logger.LogInformation("Job {JobId} started, attempt {Attempt}", job.Id, job.Attempts);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await handler.RunAsync(context, running.Token);
            await FinishAsync(job, JobState.Done, null, CancellationToken.None);
            logger.LogInformation("Job {JobId} done in {ElapsedMs} ms", job.Id, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A graceful stop is not a failed attempt: give the job back for the next worker.
            await RequeueAsync(job, CancellationToken.None);
            logger.LogInformation("Job {JobId} handed back to the queue on shutdown", job.Id);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await FinishAsync(job, JobState.Failed, $"Exceeded its time limit of {limit.TotalMinutes:0.#} min.", CancellationToken.None);
            logger.LogError("Job {JobId} exceeded its time limit of {Limit}", job.Id, limit);
        }
        catch (Exception error)
        {
            var final = job.Attempts >= Options.MaxAttempts;
            await FinishAsync(job, final ? JobState.Failed : JobState.Queued, error.Message, CancellationToken.None);
            logger.LogError(error, "Job {JobId} attempt {Attempt} failed; now {State}",
                job.Id, job.Attempts, final ? JobState.Failed : JobState.Queued);
        }
        finally
        {
            await heartbeatStop.CancelAsync();
            await heartbeat;
        }
    }

    private static Activity? StartActivity(ClaimedJob job)
    {
        var name = $"{job.Kind} job";
        ActivityContext.TryParse(job.TraceParent, null, out var parent);
        var activity = Source.StartActivity(name, ActivityKind.Consumer, parent);
        if (activity is not null) return activity;

        // No listener (no Application Insights): still run in the request's trace, so the
        // trace id in every log line matches the one the user saw.
        activity = new Activity(name);
        if (job.TraceParent is not null) activity.SetParentId(job.TraceParent);
        return activity.Start();
    }

    private async Task HeartbeatAsync(ClaimedJob job, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Options.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await ExecuteForAttemptAsync(job, "UPDATE job SET heartbeat_at = now()", cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            // Without heartbeats the sweeper will requeue the job; that is the safe outcome.
            logger.LogError(error, "Job {JobId} heartbeat stopped", job.Id);
        }
    }

    private async Task ReportAsync(ClaimedJob job, int? rowsRead, int? votesDone, int? votesTotal, CancellationToken cancellationToken)
    {
        await ExecuteForAttemptAsync(job, """
            UPDATE job SET rows_read = coalesce(@rowsRead, rows_read), votes_done = coalesce(@votesDone, votes_done),
                           votes_total = coalesce(@votesTotal, votes_total), heartbeat_at = now()
            """, cancellationToken,
            new NpgsqlParameter<int?>("rowsRead", rowsRead) { DataTypeName = "integer" },
            new NpgsqlParameter<int?>("votesDone", votesDone) { DataTypeName = "integer" },
            new NpgsqlParameter<int?>("votesTotal", votesTotal) { DataTypeName = "integer" });
    }

    private Task FinishAsync(ClaimedJob job, JobState state, string? error, CancellationToken cancellationToken) =>
        ExecuteForAttemptAsync(job, """
            UPDATE job SET state = @state, last_error = @error, heartbeat_at = now(),
                           finished_at = CASE WHEN @state IN ('done', 'failed') THEN now() END
            """, cancellationToken,
            new NpgsqlParameter("state", SnakeCaseEnumConverter<JobState>.ToName(state)),
            new NpgsqlParameter<string?>("error", error) { DataTypeName = "text" });

    private Task RequeueAsync(ClaimedJob job, CancellationToken cancellationToken) =>
        ExecuteForAttemptAsync(job, "UPDATE job SET state = 'queued', attempts = attempts - 1, heartbeat_at = NULL",
            cancellationToken);

    /// <summary>Runs an update on the job only while this worker still holds this attempt.</summary>
    private async Task ExecuteForAttemptAsync(ClaimedJob job, string update, CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = dataSource.CreateCommand($"{update} WHERE id = @id AND state = 'running' AND attempts = @attempt");
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("attempt", job.Attempts);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
