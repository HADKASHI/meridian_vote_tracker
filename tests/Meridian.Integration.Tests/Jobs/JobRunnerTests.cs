using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Jobs;

public sealed class JobRunnerTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_queued_no_op_job_runs_to_done_and_shows_in_status()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var jobId = await QueueImportJobAsync(api.ConnectionString);
        var client = api.ClientAs("dana");

        var queued = await client.GetFromJsonAsync<JsonElement>("/api/status", Ct);
        var entry = Assert.Single(queued.GetProperty("queued").EnumerateArray());
        Assert.Equal(jobId, entry.GetProperty("id").GetInt64());
        Assert.Equal(1, entry.GetProperty("placeInQueue").GetInt32());
        Assert.Equal("dana", entry.GetProperty("startedBy").GetString());

        using var worker = WorkerHost.Build(api.ConnectionString, [new TestJobHandler(JobKind.Import)]);
        Assert.Equal(1, await worker.Processor().RunUntilIdleAsync(Ct));

        var status = await client.GetFromJsonAsync<JsonElement>("/api/status", Ct);
        Assert.Empty(status.GetProperty("queued").EnumerateArray());
        Assert.Empty(status.GetProperty("running").EnumerateArray());
        var finished = Assert.Single(status.GetProperty("recentlyFinished").EnumerateArray());
        Assert.Equal("done", finished.GetProperty("state").GetString());
        Assert.Equal(0, status.GetProperty("pendingConflicts").GetInt32());

        var jobs = await client.GetFromJsonAsync<JsonElement>("/api/jobs", Ct);
        Assert.Equal("done", Assert.Single(jobs.GetProperty("items").EnumerateArray()).GetProperty("state").GetString());
    }

    [Fact]
    public async Task One_import_runs_at_a_time_while_a_snapshot_runs_alongside()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var firstImport = await QueueImportJobAsync(connectionString);
        var secondImport = await QueueImportJobAsync(connectionString);
        var snapshot = await QueueSnapshotJobAsync(connectionString);
        using var worker = WorkerHost.Build(connectionString, []);
        var processor = worker.Processor();

        Assert.Equal(firstImport, (await processor.ClaimNextAsync(Ct))?.Id);
        Assert.Equal(snapshot, (await processor.ClaimNextAsync(Ct))?.Id);
        Assert.Null(await processor.ClaimNextAsync(Ct));

        Assert.Equal(JobState.Queued, (await JobAsync(connectionString, secondImport)).State);
    }

    [Fact]
    public async Task Imports_are_claimed_in_upload_order_up_to_the_configured_limit()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var ids = new[] { await QueueImportJobAsync(connectionString), await QueueImportJobAsync(connectionString), await QueueImportJobAsync(connectionString) };
        using var worker = WorkerHost.Build(connectionString, [], new Dictionary<string, string?> { ["Imports:MaxConcurrent"] = "2" });

        Assert.Equal(ids[0], (await worker.Processor().ClaimNextAsync(Ct))?.Id);
        Assert.Equal(ids[1], (await worker.Processor().ClaimNextAsync(Ct))?.Id);
        Assert.Null(await worker.Processor().ClaimNextAsync(Ct));
    }

    [Fact]
    public async Task The_sweeper_requeues_a_silent_job_and_fails_it_after_the_last_attempt()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var jobId = await QueueImportJobAsync(connectionString);
        using var worker = WorkerHost.Build(connectionString, [], new Dictionary<string, string?> { ["Jobs:StaleAfter"] = "00:00:00.500" });
        var processor = worker.Processor();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.Equal(jobId, (await processor.ClaimNextAsync(Ct))?.Id);
            await Task.Delay(700, Ct);
            Assert.Equal(1, await processor.SweepAsync(Ct));
        }

        var job = await JobAsync(connectionString, jobId);
        Assert.Equal(JobState.Failed, job.State);
        Assert.Equal(3, job.Attempts);
        Assert.Contains("heartbeat", job.LastError);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task A_job_that_keeps_failing_fails_with_its_error_and_does_not_block_the_next()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var failing = await QueueImportJobAsync(connectionString);
        var next = await QueueImportJobAsync(connectionString);
        var calls = 0;
        using var worker = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Import, (context, _) =>
            Interlocked.Increment(ref calls) <= 3 && context.JobId == failing
                ? throw new InvalidOperationException("Row 812 broke the parser.")
                : Task.CompletedTask)]);

        await worker.Processor().RunUntilIdleAsync(Ct);

        var failed = await JobAsync(connectionString, failing);
        Assert.Equal(JobState.Failed, failed.State);
        Assert.Equal(3, failed.Attempts);
        Assert.Equal("Row 812 broke the parser.", failed.LastError);
        Assert.Equal(JobState.Done, (await JobAsync(connectionString, next)).State);
    }

    [Fact]
    public async Task Progress_reported_by_a_handler_is_stored_on_the_job()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var jobId = await QueueImportJobAsync(connectionString);
        using var worker = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Import, async (context, ct) =>
        {
            await context.ReportAsync(rowsRead: 5960, votesTotal: 40, cancellationToken: ct);
            await context.ReportAsync(votesDone: 12, cancellationToken: ct);
        })]);

        await worker.Processor().RunUntilIdleAsync(Ct);

        var job = await JobAsync(connectionString, jobId);
        Assert.Equal((5960, 12, 40), (job.RowsRead, job.VotesDone, job.VotesTotal));
    }

    [Fact]
    public async Task A_job_exceeding_its_time_limit_fails()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var jobId = await QueueImportJobAsync(connectionString);
        using var worker = WorkerHost.Build(connectionString,
            [new TestJobHandler(JobKind.Import, (_, ct) => Task.Delay(Timeout.Infinite, ct))],
            new Dictionary<string, string?> { ["Jobs:ImportTimeout"] = "00:00:00.300" });

        await worker.Processor().RunUntilIdleAsync(Ct);

        var job = await JobAsync(connectionString, jobId);
        Assert.Equal(JobState.Failed, job.State);
        Assert.Contains("time limit", job.LastError);
    }

    [Fact]
    public async Task A_killed_worker_s_job_is_requeued_by_the_sweeper_and_finished_by_another()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var jobId = await QueueImportJobAsync(connectionString);
        var started = new TaskCompletionSource();
        var settings = new Dictionary<string, string?> { ["Jobs:StaleAfter"] = "00:00:01" };

        // The first worker hangs mid-job and ignores cancellation, like a process that died.
        var hung = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Import, (_, _) =>
        {
            started.TrySetResult();
            return new TaskCompletionSource().Task;
        })], settings);
        await hung.StartAsync(Ct);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await hung.StopAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(200)).Token);
        Assert.Equal(JobState.Running, (await JobAsync(connectionString, jobId)).State);

        using var second = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Import)], settings);
        await second.StartAsync(Ct);
        var job = await WaitForAsync(connectionString, jobId, JobState.Done, TimeSpan.FromSeconds(15));
        await second.StopAsync(Ct);

        Assert.Equal(2, job.Attempts);
    }

    [Fact]
    public async Task A_job_runs_in_the_trace_of_the_request_that_queued_it()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        string? queuedIn;
        long jobId;
        using (var request = new Activity("upload").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            queuedIn = request.TraceId.ToHexString();
            jobId = await QueueImportJobAsync(connectionString);
        }
        string? ranIn = null;
        using var worker = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Import, (_, _) =>
        {
            ranIn = Activity.Current?.TraceId.ToHexString();
            return Task.CompletedTask;
        })]);

        await worker.Processor().RunUntilIdleAsync(Ct);

        Assert.Equal(queuedIn, ranIn);
        Assert.StartsWith($"00-{queuedIn}-", (await JobAsync(connectionString, jobId)).TraceParent);
    }

    private static async Task<long> QueueImportJobAsync(string connectionString)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var import = new Import { FileName = "export.csv", BlobName = "imports/x/export.csv", Sha256 = new string('0', 64), SizeBytes = 1, UploadedBy = "dana" };
        db.Imports.Add(import);
        await db.SaveChangesAsync(Ct);
        var job = JobQueue.ForImport(import.Id);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(Ct);
        return job.Id;
    }

    private static async Task<long> QueueSnapshotJobAsync(string connectionString)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var snapshot = new Snapshot { Name = "Q1", Selection = "{}", ScoringRulesVersion = "1", CreatedBy = "omar" };
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(Ct);
        var job = JobQueue.ForSnapshot(snapshot.Id);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(Ct);
        return job.Id;
    }

    private static async Task<Job> JobAsync(string connectionString, long id)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        return await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == id, Ct);
    }

    private static async Task<Job> WaitForAsync(string connectionString, long id, JobState state, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (true)
        {
            var job = await JobAsync(connectionString, id);
            if (job.State == state || DateTime.UtcNow > deadline) return job;
            await Task.Delay(100, Ct);
        }
    }
}
