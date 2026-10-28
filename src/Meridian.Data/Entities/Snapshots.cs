using Meridian.Domain;

namespace Meridian.Data.Entities;

public sealed class Snapshot
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public string? Note { get; init; }
    /// <summary>JSON of the selection as entered, including removed votes.</summary>
    public required string Selection { get; init; }
    public required string ScoringRulesVersion { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class SnapshotVote
{
    public long SnapshotId { get; init; }
    public long VoteId { get; init; }
    public long DetailsRevisionId { get; init; }
    public long PositionRevisionId { get; init; }
}

public sealed class SnapshotLine
{
    public long SnapshotId { get; init; }
    public long VoteId { get; init; }
    public required string StateCode { get; init; }
    /// <summary>Null means no data.</summary>
    public long? CountryVoteRevisionId { get; init; }
    public LineCategory Category { get; init; }
}

public sealed class SnapshotResult
{
    public long SnapshotId { get; init; }
    public required string StateCode { get; init; }
    public int MatchedWeight { get; init; }
    public int CountedWeight { get; init; }
    /// <summary>Null exactly when nothing was countable.</summary>
    public int? Rank { get; init; }
}

public sealed class SnapshotWarning
{
    public long Id { get; init; }
    public long SnapshotId { get; init; }
    public required string Kind { get; init; }
    public long? VoteId { get; init; }
    public required string Detail { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>An insert-only chain per reporting quarter; the end of the chain is the published snapshot.</summary>
public sealed class Publication
{
    public long Id { get; init; }
    public long SnapshotId { get; init; }
    /// <summary><c>YYYY-Qn</c>.</summary>
    public required string Quarter { get; init; }
    public required string PublishedBy { get; init; }
    /// <summary>Required exactly when superseding.</summary>
    public string? Reason { get; init; }
    public long? SupersedesPublicationId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
