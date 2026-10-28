using Meridian.Domain;
using Meridian.Platform.Contracts;

namespace Meridian.Votes;

public sealed record VoteSummaryDto(long Id, string AssemblyVoteId, int Session, DateOnly VoteDate, string Title, PositionDto? Position);

public sealed record VoteDetailsDto(long RevisionId, int Session, DateOnly VoteDate, string Title, OriginDto Origin);

/// <summary>One member state in a vote: its approved country vote (or none) and whether it was a member on the date.</summary>
public sealed record CountryVoteDto(
    string StateCode, string StateName, Ballot? Ballot, long? RevisionId, OriginDto? Origin, bool MemberOnVoteDate);

/// <param name="Details">Null for a vote seen only in a contradiction, which has no approved details.</param>
public sealed record VoteDto(
    long Id, string AssemblyVoteId, VoteDetailsDto? Details, PositionDto? Position,
    IReadOnlyList<CountryVoteDto> CountryVotes, int PendingConflicts);

public enum TimelineEventKind
{
    Import, DetailsRevision, CountryVoteRevision, PositionRevision,
    ConflictOpened, ConflictDecided, DecisionNotApplied, Snapshot, Publication,
}

/// <summary>A value as the timeline shows it in from → to.</summary>
public sealed record TimelineValueDto(Ballot? Ballot, int? Session, DateOnly? VoteDate, string? Title, Stance? Stance, short? Weight);

/// <summary>One event on a vote's timeline. Which fields are set depends on <see cref="Kind"/>.</summary>
/// <param name="From">For revisions: the previous value for the same subject, computed by the API.</param>
public sealed record TimelineEventDto(
    long HistoryId, TimelineEventKind Kind, string Actor, DateTimeOffset At,
    string? StateCode, TimelineValueDto? From, TimelineValueDto? To, OriginDto? Origin,
    long? ImportId, string? FileName, OutcomeCountsDto? Counts,
    long? ConflictId, ConflictSubject? Subject, Decision? Decision, string? Comment,
    long? SnapshotId, string? SnapshotName, long? PublicationId, string? Quarter, string? Reason);

/// <param name="NextBefore">Pass as <c>before</c> to read the next (older) page; null on the last page.</param>
public sealed record TimelinePageDto(IReadOnlyList<TimelineEventDto> Items, long? NextBefore);
