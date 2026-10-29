using System.Diagnostics;
using Meridian.Data.Entities;
using Meridian.Domain;

namespace Meridian.Platform.Jobs;

/// <summary>
/// The work of one job kind. Slices plug theirs in (the import handler in slice 1, the
/// snapshot handler in slice 6); the runner owns claiming, heartbeats, retries and limits.
/// A handler may run again after a crash, so it must resume from what it committed.
/// </summary>
public interface IJobHandler
{
    JobKind Kind { get; }

    /// <summary>
    /// Does the job's work. Throwing fails this attempt. A <see cref="JobFailedException"/>'s
    /// message becomes the job's <c>last_error</c>; any other exception is recorded by its
    /// type only, because its message may quote client data.
    /// </summary>
    Task RunAsync(JobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A failure a handler explains in words safe to show users and write to logs: ids and
/// counts only, never raw rows, titles, ballots or comments.
/// </summary>
public sealed class JobFailedException(string message) : Exception(message);

/// <summary>What a handler knows about the job it runs, and how it reports progress.</summary>
public sealed class JobContext(ClaimedJob job, Func<int?, int?, int?, CancellationToken, Task> report)
{
    public long JobId => job.Id;
    public JobKind Kind => job.Kind;
    public long? ImportId => job.ImportId;
    public long? SnapshotId => job.SnapshotId;

    /// <summary>1 on the first run, higher when retried after a failure or a stall.</summary>
    public int Attempt => job.Attempts;

    /// <summary>Stores progress counters (null leaves one as it is); also counts as a heartbeat.</summary>
    public Task ReportAsync(int? rowsRead = null, int? votesDone = null, int? votesTotal = null,
        CancellationToken cancellationToken = default) =>
        report(rowsRead, votesDone, votesTotal, cancellationToken);
}

/// <summary>A job this worker has claimed and is running.</summary>
public sealed record ClaimedJob(long Id, JobKind Kind, long? ImportId, long? SnapshotId, int Attempts, string? TraceParent);

/// <summary>Creates queued jobs. The caller inserts one in the same transaction as its import or snapshot.</summary>
public static class JobQueue
{
    public static Job ForImport(long importId) =>
        new() { Kind = JobKind.Import, ImportId = importId, State = JobState.Queued, TraceParent = CurrentTraceParent() };

    public static Job ForSnapshot(long snapshotId) =>
        new() { Kind = JobKind.Snapshot, SnapshotId = snapshotId, State = JobState.Queued, TraceParent = CurrentTraceParent() };

    /// <summary>The W3C traceparent of the current request, so the job runs in its trace.</summary>
    private static string? CurrentTraceParent() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.Id : null;
}

/// <summary>The <c>Jobs</c> and <c>Imports</c> settings that govern the runner.</summary>
public sealed class JobOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan ImportTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan SnapshotTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary><c>Imports:MaxConcurrent</c>: how many imports may run at once (1, so the earlier upload is approved first).</summary>
    public int MaxConcurrentImports { get; set; } = 1;
}

/// <summary>
/// What the runner needs to know about one job kind: how many may run at once, how long one
/// may take, and which id its log lines carry. A new kind is one entry in <see cref="JobPolicies.For"/>.
/// </summary>
public sealed record JobKindPolicy(
    JobKind Kind, int MaxRunning, TimeSpan Timeout, string LogScopeKey, Func<ClaimedJob, long?> LinkId);

public static class JobPolicies
{
    /// <summary>A <see cref="JobKind"/> missing here is caught by the policy test, which asks every kind for its policy.</summary>
    public static JobKindPolicy For(JobKind kind, JobOptions options) => kind switch
    {
        JobKind.Import => new(kind, options.MaxConcurrentImports, options.ImportTimeout, "ImportId", job => job.ImportId),
        JobKind.Snapshot => new(kind, 1, options.SnapshotTimeout, "SnapshotId", job => job.SnapshotId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No job policy for this kind."),
    };

    public static IEnumerable<JobKindPolicy> All(JobOptions options) =>
        Enum.GetValues<JobKind>().Select(kind => For(kind, options));
}
