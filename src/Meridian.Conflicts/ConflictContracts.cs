using System.ComponentModel.DataAnnotations;
using Meridian.Domain;
using Meridian.Platform.Contracts;

namespace Meridian.Conflicts;

/// <summary>A value on either side of a conflict: a ballot, or vote details.</summary>
public sealed record ConflictValueDto(Ballot? Ballot, int? Session, DateOnly? VoteDate, string? Title);

public sealed record ConflictSideDto(ConflictValueDto Value, long RevisionId, OriginDto Origin);

/// <summary>The kinds of item in the Review inbox; a later PDF extraction item joins here.</summary>
public enum InboxItemKind { Conflict }

/// <param name="Current">The approved value now, read live; null if it is gone.</param>
public sealed record ConflictListItemDto(
    InboxItemKind Kind, long Id, VoteRefDto Vote, ConflictSubject Subject, string? StateCode,
    ConflictValueDto? Current, ConflictValueDto Incoming, long ImportId, DateTimeOffset OpenedAt, Decision? Decision);

/// <summary>A decision with its from → to, as the winner's card shows it to the loser of a race.</summary>
public sealed record ConflictDecisionDto(
    long Id, long ConflictId, Decision Decision, string DecidedBy, DateTimeOffset DecidedAt, string? Comment,
    ConflictValueDto From, ConflictValueDto To);

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
