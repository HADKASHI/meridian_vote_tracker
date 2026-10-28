using Meridian.Domain;

namespace Meridian.Data.Entities;

/// <summary>Created the first time a well-formed assembly vote id is seen; the per-vote lock target.</summary>
public sealed class Vote
{
    public long Id { get; init; }
    public required string AssemblyVoteId { get; init; }
    public long FirstSeenImportId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class VoteDetailsRevision
{
    public long Id { get; init; }
    public long VoteId { get; init; }
    public int Session { get; init; }
    public DateOnly VoteDate { get; init; }
    public required string Title { get; init; }
    public long? OriginImportRowId { get; init; }
    public long? OriginDecisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class PositionRevision
{
    public long Id { get; init; }
    public long VoteId { get; init; }
    public Stance Stance { get; init; }
    public short Weight { get; init; }
    public required string SetBy { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class CountryVoteRevision
{
    public long Id { get; init; }
    public long VoteId { get; init; }
    public required string StateCode { get; init; }
    public Ballot Ballot { get; init; }
    public long? OriginImportRowId { get; init; }
    public long? OriginDecisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>Pointer to a vote's approved details and position. One of the few rows that move.</summary>
public sealed class VoteCurrent
{
    public long VoteId { get; init; }
    public long DetailsRevisionId { get; set; }
    /// <summary>Null means no position yet.</summary>
    public long? PositionRevisionId { get; set; }
}

/// <summary>Pointer to a member state's approved country vote in a vote.</summary>
public sealed class CountryVoteCurrent
{
    public long VoteId { get; init; }
    public required string StateCode { get; init; }
    public long RevisionId { get; set; }
}
