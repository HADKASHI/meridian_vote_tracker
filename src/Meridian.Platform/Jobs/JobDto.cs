using Meridian.Domain;

namespace Meridian.Platform.Jobs;

/// <summary>A job as every screen shows it: queued (with place in queue), running with progress, done or failed.</summary>
/// <param name="PlaceInQueue">1 for the next job of its kind to run; null unless queued.</param>
/// <param name="TraceId">The trace id that finds the job's work in the logs.</param>
/// <param name="StartedBy">The user whose upload or snapshot queued the job.</param>
public sealed record JobDto(
    long Id,
    JobKind Kind,
    JobState State,
    long? ImportId,
    long? SnapshotId,
    int? PlaceInQueue,
    int RowsRead,
    int VotesDone,
    int? VotesTotal,
    int Attempts,
    string? LastError,
    string? TraceId,
    string StartedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
