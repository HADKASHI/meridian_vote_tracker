using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;

namespace Meridian.Extractions;

/// <param name="Existing">True when these exact bytes were uploaded before and this is that earlier extraction.</param>
public sealed record ExtractionCreatedDto(long ExtractionId, long JobId, bool Existing);

/// <param name="VoteId">The vote number printed on the sheet, once it has been read.</param>
public sealed record ExtractionSummaryDto(
    long Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, long SizeBytes, ExtractionStatus Status,
    string? VoteId, JobDto Job, int BlockingIssues, int Warnings);

/// <summary>A sheet as the analyst reviews it: what was read, the counts against the printed totals, and what blocks or warns.</summary>
/// <param name="Error">Why extraction failed, in words free of the sheet's content.</param>
/// <param name="Counted">What the rows add up to; null until the sheet has been read.</param>
/// <param name="Printed">The totals the sheet prints; null when they could not be read.</param>
/// <param name="CanApprove">False while any issue blocks.</param>
/// <param name="Edits">What the analyst changed, oldest first, each with the value it replaced.</param>
/// <param name="Decision">Who approved or rejected it and when; null while it waits.</param>
/// <param name="ImportId">The import an approval made from the sheet.</param>
public sealed record ExtractionDto(
    long Id, string FileName, string Sha256, long SizeBytes, string UploadedBy, DateTimeOffset UploadedAt,
    ExtractionStatus Status, JobDto Job, string? Error, string? VoteId, SheetHeader? Header,
    IReadOnlyList<SheetRow> Rows, SheetTotals? Counted, SheetTotals? Printed, IReadOnlyList<SheetIssue> Issues, bool CanApprove,
    IReadOnlyList<ExtractionEditDto> Edits, ExtractionDecisionDto? Decision, long? ImportId);

/// <param name="Rows">Corrections to grid rows by position, or the removal of a row that has an issue.</param>
/// <param name="Header">Corrections to the printed header fields.</param>
/// <param name="Printed">The totals the sheet prints, when they were misread or missing.</param>
/// <param name="AcceptTotals">True to accept that the rows count to something other than the printed totals, as the sheet stands after these edits.</param>
public sealed record SaveEditsRequest(IReadOnlyList<RowEdit>? Rows, HeaderEdit? Header, SheetTotals? Printed, bool? AcceptTotals = null);

public sealed record RejectExtractionRequest(string? Comment);

/// <summary>One change by an analyst: the field (<c>code</c>, <c>choice</c>, <c>removed</c>, <c>totalsAccepted</c>, <c>voteId</c>, <c>session</c>, <c>date</c>, <c>title</c>, <c>printed</c>), the row, and the value before and after.</summary>
public sealed record ExtractionEditDto(string By, DateTimeOffset At, string Field, int? Position, string? Code, string? From, string? To);

public sealed record ExtractionDecisionDto(ExtractionStatus Status, string DecidedBy, DateTimeOffset DecidedAt, string? Comment);

/// <summary>The 409 for a second decision: the decision that won.</summary>
public sealed class ExtractionAlreadyDecidedProblem : Meridian.Platform.Contracts.Problem
{
    public required ExtractionDecisionDto Decision { get; init; }
}

public sealed record ApproveExtractionRequest(string? Comment);

/// <param name="ImportId">The import the approval made; its report says which rows were new, unchanged or in conflict.</param>
public sealed record ExtractionApprovedDto(ExtractionDecisionDto Decision, long ImportId);

/// <summary>What approving the sheet now would do, by the importer's own rules; nothing is written to get it.</summary>
/// <param name="Counts">How many rows would be new, unchanged, in conflict and so on.</param>
/// <param name="VoteExists">False when the vote has no approved data yet, so every row would be new.</param>
/// <param name="ConflictStateCodes">The states whose approved ballot the sheet disagrees with: one conflict each.</param>
public sealed record ExtractionPreviewDto(OutcomeCountsDto Counts, bool VoteExists, IReadOnlyList<string> ConflictStateCodes);
