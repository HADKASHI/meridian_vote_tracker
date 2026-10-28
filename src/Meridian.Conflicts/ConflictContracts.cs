using System.ComponentModel.DataAnnotations;
using Meridian.Domain;
using Meridian.Platform.Contracts;

namespace Meridian.Conflicts;

/// <summary>A value on either side of a conflict: a ballot, or vote details.</summary>
public sealed record ConflictValueDto(Ballot? Ballot, int? Session, DateOnly? VoteDate, string? Title);

/// <param name="RevisionId">The approved revision holding the value; null for an incoming value not yet approved.</param>
public sealed record ConflictSideDto(ConflictValueDto Value, long? RevisionId, OriginDto Origin);

/// <summary>The kinds of item in the Review inbox; a later PDF extraction item joins here.</summary>
public enum InboxItemKind { Conflict }

/// <param name="Current">The approved value now, read live; null if it is gone.</param>
public sealed record ConflictListItemDto(
    InboxItemKind Kind, long Id, VoteRefDto Vote, ConflictSubject Subject, string? StateCode,
    ConflictValueDto? Current, ConflictValueDto Incoming, long ImportId, DateTimeOffset OpenedAt, Decision? Decision);

/// <summary>What a vote-details decision's membership recalculation did to the vote's ballots.</summary>
/// <param name="ApprovedStateCodes">States whose ballot, invalid at import only for membership, the decision approved.</param>
/// <param name="NoLongerCountedStateCodes">States whose approved ballot stays stored but no longer counts, being outside membership on the new date.</param>
/// <param name="OpenedConflictIds">Ballot conflicts opened for later rows that gave an approved state a different ballot.</param>
public sealed record MembershipRecalculationDto(
    IReadOnlyList<string> ApprovedStateCodes, IReadOnlyList<string> NoLongerCountedStateCodes, IReadOnlyList<long> OpenedConflictIds);

/// <summary>A decision with its from → to, as the winner's card shows it to the loser of a race.</summary>
/// <param name="From">The current value when it was decided.</param>
/// <param name="To">The incoming value it accepted or rejected.</param>
/// <param name="Membership">For a vote-details decision, what recalculating membership did; null for a ballot.</param>
public sealed record ConflictDecisionDto(
    long Id, long ConflictId, Decision Decision, string DecidedBy, DateTimeOffset DecidedAt, string? Comment,
    ConflictValueDto From, ConflictValueDto To, MembershipRecalculationDto? Membership);

public sealed record NotAppliedAttemptDto(long Id, string AttemptedBy, Decision Attempted, DateTimeOffset AttemptedAt);

/// <summary>The decision pane: both values with their origins.</summary>
/// <param name="Incoming">The incoming value; its origin is the first file row that brought it.</param>
public sealed record ConflictDto(
    long Id, VoteRefDto Vote, ConflictSubject Subject, string? StateCode,
    ConflictSideDto? Current, ConflictSideDto Incoming, long ComparedRevisionId,
    ConflictDecisionDto? Decision, IReadOnlyList<NotAppliedAttemptDto> NotApplied);

public sealed class DecideConflictRequest
{
    [Required] public Decision? Decision { get; init; }
    [MaxLength(2000)] public string? Comment { get; init; }
}

/// <summary><c>already-decided</c> (409): someone else decided first; their decision.</summary>
public sealed class AlreadyDecidedProblem : Problem
{
    public required ConflictDecisionDto Decision { get; init; }
}
