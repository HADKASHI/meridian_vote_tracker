using System.ComponentModel.DataAnnotations;
using Meridian.Domain;
using Meridian.Domain.Scoring;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;

namespace Meridian.Snapshots;

/// <summary>
/// Which votes a snapshot covers: an inclusive date range on approved dates, one or more
/// sessions, or an explicit list, minus any votes removed in the preview.
/// </summary>
public sealed class SnapshotSelectionDto : IValidatableObject
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public IReadOnlyList<int>? Sessions { get; init; }
    public IReadOnlyList<long>? VoteIds { get; init; }

    /// <summary>Votes removed with "Remove these votes", recorded as "range minus these votes".</summary>
    public IReadOnlyList<long>? ExcludedVoteIds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var byRange = From is not null || To is not null;
        var criteria = (byRange ? 1 : 0) + (Sessions is not null ? 1 : 0) + (VoteIds is not null ? 1 : 0);
        if (criteria != 1)
        {
            yield return new ValidationResult("Choose one of a date range, sessions or a list of votes.");
            yield break;
        }

        if (byRange)
        {
            if (From is null) yield return new ValidationResult("Enter the first date.", [nameof(From)]);
            if (To is null) yield return new ValidationResult("Enter the last date.", [nameof(To)]);
            if (From > To) yield return new ValidationResult("The last date is before the first.", [nameof(To)]);
        }

        if (Sessions is { Count: 0 }) yield return new ValidationResult("Choose at least one session.", [nameof(Sessions)]);
        for (var i = 0; i < (Sessions?.Count ?? 0); i++)
            if (Sessions![i] <= 0) yield return new ValidationResult("A session is a positive number.", [$"{nameof(Sessions)}[{i}]"]);

        if (VoteIds is { Count: 0 }) yield return new ValidationResult("Choose at least one vote.", [nameof(VoteIds)]);
    }
}

public sealed class CreateSnapshotRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = false), MaxLength(200)] public string? Name { get; init; }
    [MaxLength(2000)] public string? Note { get; init; }
    [Required] public SnapshotSelectionDto? Selection { get; init; }

    /// <summary>The id of a rule set; omitted means the default. Only the admin may choose another.</summary>
    public string? RuleSet { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RuleSet is not null && !RuleSets.TryGet(RuleSet, out _))
            yield return new ValidationResult($"{RuleSet} is not a known rule set.", [nameof(RuleSet)]);
    }
}

/// <summary>A rule set (ADR 0007). Weights in results are points of 1 ÷ <c>WeightUnit</c> of a weight.</summary>
public sealed record RuleSetDto(string Id, string Name, string Description, int WeightUnit, bool IsDefault)
{
    public static RuleSetDto Of(RuleSet ruleSet) =>
        new(ruleSet.Id, ruleSet.Name, ruleSet.Description, ruleSet.WeightUnit, ruleSet.Id == RuleSets.DefaultId);

    /// <summary>A snapshot's rule set by the id it stored; an id no rule set has (a fixture-built snapshot) reads as the default's rules.</summary>
    public static RuleSetDto Stored(string id) =>
        RuleSets.TryGet(id, out var known) ? Of(known) : new RuleSetDto(id, id, "Not a known rule set; read as the default's rules.", 1, false);
}

public enum SnapshotWarningKind { PendingConflicts, IncompleteVote, ImportRunning }

/// <param name="ImportId">For <c>import_running</c>: the import that was applying, and how far it was.</param>
public sealed record SnapshotWarningDto(
    SnapshotWarningKind Kind, string Message, VoteRefDto? Vote,
    int? PendingConflicts, IReadOnlyList<string>? MissingStateCodes,
    long? ImportId, int? VotesDone, int? VotesTotal);

/// <param name="NoPosition">Votes without a position: they block the snapshot until set or removed.</param>
public sealed record SnapshotPreviewDto(int VoteCount, IReadOnlyList<VoteRefDto> NoPosition, IReadOnlyList<SnapshotWarningDto> Warnings);

public sealed record SnapshotCreatedDto(long SnapshotId, long JobId);

/// <summary>
/// One state's result. Score is 100 × matched ÷ counted, null (n/a) when nothing was countable. The evidence is a share (0–1)
/// of the votes the state was a member for, derived from the stored lines and the same under every rule set (ADR 0007):
/// completeness is yes and no ballots, abstention rate is abstentions, missing data is votes with no data. They are null when
/// the state was never a member, and <c>Confidence</c> is null when nothing was countable.
/// </summary>
public sealed record SnapshotResultDto(
    string StateCode, string StateName, int MatchedWeight, int CountedWeight, int CountableVotes, double? Score, int? Rank,
    double? Completeness, double? AbstentionRate, double? MissingData, Confidence? Confidence);

public sealed record PublicationDto(
    long Id, long SnapshotId, string SnapshotName, string Quarter, string PublishedBy, DateTimeOffset PublishedAt,
    string? Reason, long? SupersedesPublicationId, long? SupersededByPublicationId, string RuleSetId, string RuleSetName);

public sealed record SnapshotSummaryDto(
    long Id, string Name, string CreatedBy, DateTimeOffset CreatedAt, int? VoteCount, JobDto Job,
    IReadOnlyList<PublicationDto> Publications);

public sealed record SnapshotDto(
    long Id, string Name, string? Note, SnapshotSelectionDto Selection, string ScoringRulesVersion, RuleSetDto RuleSet,
    string CreatedBy, DateTimeOffset CreatedAt, JobDto Job, int VoteCount,
    IReadOnlyList<SnapshotResultDto> Results, IReadOnlyList<SnapshotWarningDto> Warnings,
    IReadOnlyList<PublicationDto> Publications);

/// <summary>One vote in a state's breakdown: the exact value used and how it was treated.</summary>
public sealed record SnapshotLineDto(
    VoteRefDto Vote, DateOnly VoteDate, string Title, Stance Stance, short Weight,
    Ballot? Ballot, LineCategory Category, long? CountryVoteRevisionId);

public sealed record SnapshotBreakdownDto(SnapshotResultDto Result, IReadOnlyList<SnapshotLineDto> Lines);

public sealed record SnapshotRefDto(long Id, string Name, DateTimeOffset CreatedAt, RuleSetDto RuleSet);

/// <summary>What a score rests on: countable votes and the weight matched out of the weight counted.</summary>
public sealed record ScoreBasisDto(int CountableVotes, int MatchedWeight, int CountedWeight);

/// <summary>Whether a vote is in both snapshots or only one, for a reason a state moved.</summary>
public enum ReasonKind { OnlyInA, OnlyInB, Changed }

/// <summary>What differs for a vote in both snapshots.</summary>
/// <param name="Rules">Category, ballot, position and weight are the same, but the two rule sets count it differently.</param>
public enum ReasonChange { Category, Ballot, Stance, Weight, Rules }

/// <summary>How one snapshot treated a vote for a state: the category, the ballot it used and the position.</summary>
public sealed record ReasonSideDto(LineCategory Category, Ballot? Ballot, Stance Stance, short Weight);

/// <summary>
/// A vote that changed what a state matched or counted between A and B. <c>MatchedChange</c> and
/// <c>CountedChange</c> are B minus A, so a state's reasons add up to its change in matched and counted weight.
/// </summary>
/// <param name="A">How A treated the vote; null for a vote only in B.</param>
/// <param name="B">How B treated the vote; null for a vote only in A.</param>
public sealed record MovementReasonDto(
    VoteRefDto Vote, string Title, ReasonKind Kind, IReadOnlyList<ReasonChange> Changes,
    ReasonSideDto? A, ReasonSideDto? B, int MatchedChange, int CountedChange);

/// <param name="BasisA">What the score in A rests on; null when the state has no result in A.</param>
/// <param name="Reasons">The votes that explain the movement; empty when the state's weights did not change.</param>
public sealed record StateMovementDto(
    string StateCode, string StateName, double? ScoreA, int? RankA, ScoreBasisDto? BasisA,
    double? ScoreB, int? RankB, ScoreBasisDto? BasisB, double? ScoreChange, int? RankChange,
    IReadOnlyList<MovementReasonDto> Reasons);

/// <summary>
/// <c>RuleSetsDiffer</c> is the warning that the movement mixes a change of rules with a change of data. Every weight in the
/// states and their reasons is in points of 1 ÷ <c>WeightUnit</c> of a weight, the finest unit of the two rule sets.
/// </summary>
public sealed record SnapshotComparisonDto(
    SnapshotRefDto A, SnapshotRefDto B, bool RuleSetsDiffer, int WeightUnit, IReadOnlyList<StateMovementDto> States);

/// <summary>What changed a value: a person setting a position, an import row, or a conflict decision.</summary>
public enum ChangeOriginKind { Position, Import, Decision }

/// <summary>Who or what changed a value: <c>By</c> is the admin, the uploader or the person who decided.</summary>
public sealed record ChangeOriginDto(ChangeOriginKind Kind, string By, long? ImportId, long? DecisionId);

public sealed record PositionValueDto(Stance Stance, short Weight);

public sealed record DetailsValueDto(int Session, DateOnly VoteDate, string Title);

public sealed record PositionChangeDto(
    VoteRefDto Vote, PositionValueDto Old, PositionValueDto New, ChangeOriginDto ChangedBy, DateTimeOffset At);

public sealed record DetailsChangeDto(
    VoteRefDto Vote, DetailsValueDto Old, DetailsValueDto New, ChangeOriginDto ChangedBy, DateTimeOffset At);

/// <param name="Old">The ballot the snapshot used; null when it had no data for the state.</param>
public sealed record CountryVoteChangeDto(
    VoteRefDto Vote, string StateCode, Ballot? Old, Ballot New, ChangeOriginDto ChangedBy, DateTimeOffset At);

/// <param name="Count">Every vote that changed; <c>Votes</c> holds the first 20.</param>
public sealed record PositionChangesDto(int Count, IReadOnlyList<PositionChangeDto> Votes);

/// <inheritdoc cref="PositionChangesDto"/>
public sealed record DetailsChangesDto(int Count, IReadOnlyList<DetailsChangeDto> Votes);

/// <summary>Each entry is one member state's ballot in one vote; <c>Count</c> counts those, not votes.</summary>
public sealed record CountryVoteChangesDto(int Count, IReadOnlyList<CountryVoteChangeDto> Votes);

/// <summary>A vote that now matches the snapshot's selection but is not in it, as it stands today.</summary>
/// <param name="Position">Null when the vote has no position yet.</param>
public sealed record NowSelectedVoteDto(VoteRefDto Vote, DetailsValueDto Details, PositionValueDto? Position);

public sealed record NowSelectedDto(int Count, IReadOnlyList<NowSelectedVoteDto> Votes);

/// <summary>
/// What would differ if the snapshot were created now, read live from its stored revisions and today's
/// values. It holds no score or rank and was not calculated.
/// </summary>
public sealed record SnapshotChangesDto(DateTimeOffset ReadAt, PositionChangesDto PositionChanged,
    CountryVoteChangesDto CountryVotesChanged, DetailsChangesDto DetailsChanged,
    NowSelectedDto NowSelected, DetailsChangesDto NoLongerSelected);

public sealed class PublishSnapshotRequest : IValidatableObject
{
    [Required, RegularExpression("^[0-9]{4}-Q[1-4]$", ErrorMessage = "A reporting quarter looks like 2024-Q1.")]
    public string? Quarter { get; init; }

    /// <summary>The quarter's current publication, when replacing it.</summary>
    public long? SupersedesPublicationId { get; init; }

    [MaxLength(2000)] public string? Reason { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SupersedesPublicationId is not null && string.IsNullOrWhiteSpace(Reason))
            yield return new ValidationResult("Give a reason for replacing the published snapshot.", [nameof(Reason)]);
        if (SupersedesPublicationId is null && Reason is not null)
            yield return new ValidationResult("A reason is only given when replacing a publication.", [nameof(Reason)]);
    }
}

/// <summary>The chain of one reporting quarter: the first publication, then each replacement.</summary>
public sealed record QuarterPublicationsDto(string Quarter, PublicationDto Current, IReadOnlyList<PublicationDto> Chain);

/// <summary><c>quarter-published</c> (409): the quarter already has this publication.</summary>
public sealed class QuarterPublishedProblem : Problem
{
    public required PublicationDto Publication { get; init; }
}

/// <summary><c>no-position</c> (400): these votes have no position and block the snapshot.</summary>
public sealed class NoPositionProblem : Problem
{
    public required IReadOnlyList<VoteRefDto> Votes { get; init; }
}
