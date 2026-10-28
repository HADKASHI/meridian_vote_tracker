using Meridian.Domain;

namespace Meridian.Data.Entities;

// Keyless entities over the views created in migration 1.

/// <summary>A member state's approved country vote in a vote, through its pointer.</summary>
public sealed class CurrentCountryVote
{
    public long VoteId { get; init; }
    public required string StateCode { get; init; }
    public long RevisionId { get; init; }
    public Ballot Ballot { get; init; }
    public long? OriginImportRowId { get; init; }
    public long? OriginDecisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>A vote with its approved details and position. Only votes that have details.</summary>
public sealed class CurrentVote
{
    public long VoteId { get; init; }
    public required string AssemblyVoteId { get; init; }
    public long DetailsRevisionId { get; init; }
    public int Session { get; init; }
    public DateOnly VoteDate { get; init; }
    public required string Title { get; init; }
    public long? PositionRevisionId { get; init; }
    public Stance? Stance { get; init; }
    public short? Weight { get; init; }
}

/// <summary>A conflict with no decision, plus the live current value it is judged against.</summary>
public sealed class PendingConflict
{
    public long ConflictId { get; init; }
    public long VoteId { get; init; }
    public ConflictSubject Subject { get; init; }
    public long OpenedByImportId { get; init; }
    public string? StateCode { get; init; }
    public Ballot? IncomingBallot { get; init; }
    public long? ComparedCountryVoteRevisionId { get; init; }
    public int? IncomingSession { get; init; }
    public DateOnly? IncomingVoteDate { get; init; }
    public string? IncomingTitle { get; init; }
    public long? ComparedDetailsRevisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public long? CurrentCountryVoteRevisionId { get; init; }
    public Ballot? CurrentBallot { get; init; }
    public long? CurrentDetailsRevisionId { get; init; }
    public int? CurrentSession { get; init; }
    public DateOnly? CurrentVoteDate { get; init; }
    public string? CurrentTitle { get; init; }
}

/// <summary>One event on a vote's timeline, ordered by <see cref="HistoryId"/>.</summary>
public sealed class VoteTimelineEvent
{
    public long VoteId { get; init; }
    public long HistoryId { get; init; }
    /// <summary>
    /// <c>import</c>, <c>details_revision</c>, <c>country_vote_revision</c>, <c>position_revision</c>,
    /// <c>conflict_opened</c>, <c>conflict_decided</c>, <c>decision_not_applied</c>, <c>snapshot</c>
    /// or <c>publication</c>.
    /// </summary>
    public required string Kind { get; init; }
    public required string Actor { get; init; }
    public DateTimeOffset At { get; init; }
    /// <summary>JSON with the event's own fields.</summary>
    public required string Detail { get; init; }
}
