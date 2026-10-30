using Meridian.Domain;

namespace Meridian.Data.Entities;

/// <summary>
/// An incoming value that differs from approved data, with a link to the revision it was
/// compared against. The current value is always read live, never copied.
/// </summary>
public sealed class Conflict
{
    public long Id { get; init; }
    public long VoteId { get; init; }
    public ConflictSubject Subject { get; init; }
    public long OpenedByImportId { get; init; }

    // Ballot side.
    public string? StateCode { get; init; }
    public Ballot? IncomingBallot { get; init; }
    public long? ComparedCountryVoteRevisionId { get; init; }

    // Details side.
    public int? IncomingSession { get; init; }
    public DateOnly? IncomingVoteDate { get; init; }
    public string? IncomingTitle { get; init; }
    /// <summary>The title's comparison key.</summary>
    public string? IncomingTitleKey { get; init; }
    public long? ComparedDetailsRevisionId { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class ImportRowConflict
{
    public long ImportRowId { get; init; }
    public long ConflictId { get; init; }
}

/// <summary>At most one per conflict: the database refuses a second.</summary>
public sealed class ConflictDecision
{
    public long Id { get; init; }
    public long ConflictId { get; init; }
    public Decision Decision { get; init; }
    public required string DecidedBy { get; init; }
    public string? Comment { get; init; }
    /// <summary>The current value at decision time.</summary>
    public long? ReplacedCountryVoteRevisionId { get; init; }
    public long? ReplacedDetailsRevisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>A losing simultaneous decision, kept for the vote's timeline.</summary>
public sealed class DecisionAttemptNotApplied
{
    public long Id { get; init; }
    public long ConflictId { get; init; }
    public required string AttemptedBy { get; init; }
    public Decision Attempted { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
