using Meridian.Domain;
using Meridian.Domain.Sheets;
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
public sealed record ExtractionDto(
    long Id, string FileName, string Sha256, long SizeBytes, string UploadedBy, DateTimeOffset UploadedAt,
    ExtractionStatus Status, JobDto Job, string? Error, string? VoteId, SheetHeader? Header,
    IReadOnlyList<SheetRow> Rows, SheetTotals? Counted, SheetTotals? Printed, IReadOnlyList<SheetIssue> Issues, bool CanApprove,
    IReadOnlyList<ExtractionEditDto> Edits, ExtractionDecisionDto? Decision);

/// <param name="Rows">Corrections to grid rows by position.</param>
/// <param name="Header">Corrections to the printed header fields.</param>
/// <param name="Printed">The totals the sheet prints, when they were misread or missing.</param>
public sealed record SaveEditsRequest(IReadOnlyList<RowEdit>? Rows, HeaderEdit? Header, SheetTotals? Printed);

public sealed record RejectExtractionRequest(string? Comment);

/// <summary>One change by an analyst: the field (<c>code</c>, <c>choice</c>, <c>voteId</c>, <c>session</c>, <c>date</c>, <c>title</c>, <c>printed</c>), the row, and the value before and after.</summary>
public sealed record ExtractionEditDto(string By, DateTimeOffset At, string Field, int? Position, string? Code, string? From, string? To);

public sealed record ExtractionDecisionDto(ExtractionStatus Status, string DecidedBy, DateTimeOffset DecidedAt, string? Comment);

/// <summary>The 409 for a second decision: the decision that won.</summary>
public sealed class ExtractionAlreadyDecidedProblem : Meridian.Platform.Contracts.Problem
{
    public required ExtractionDecisionDto Decision { get; init; }
}
