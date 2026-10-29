using Meridian.Domain;

namespace Meridian.Data.Entities;

/// <summary>The one mutable queue table: work done in the background on a user's behalf.</summary>
public sealed class Job
{
    public long Id { get; init; }
    public JobKind Kind { get; init; }
    public long? ImportId { get; init; }
    public long? SnapshotId { get; init; }
    public long? ExtractionId { get; init; }
    public JobState State { get; set; }
    public int RowsRead { get; set; }
    public int VotesDone { get; set; }
    public int? VotesTotal { get; set; }
    public DateTimeOffset? HeartbeatAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    /// <summary>W3C traceparent of the request that queued the job.</summary>
    public string? TraceParent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
