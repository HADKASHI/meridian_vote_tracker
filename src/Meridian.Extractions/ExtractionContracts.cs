using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Platform.Jobs;

namespace Meridian.Extractions;

public sealed record ExtractionCreatedDto(long ExtractionId, long JobId);

/// <param name="VoteId">The vote number printed on the sheet, once it has been read.</param>
public sealed record ExtractionSummaryDto(
    long Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, long SizeBytes, ExtractionStatus Status,
    string? VoteId, JobDto Job, int BlockingIssues, int Warnings);

/// <summary>A sheet as the analyst reviews it: what was read, the counts against the printed totals, and what blocks or warns.</summary>
/// <param name="Error">Why extraction failed, in words free of the sheet's content.</param>
/// <param name="Counted">What the rows add up to; null until the sheet has been read.</param>
/// <param name="Printed">The totals the sheet prints; null when they could not be read.</param>
/// <param name="CanApprove">False while any issue blocks.</param>
public sealed record ExtractionDto(
    long Id, string FileName, string Sha256, long SizeBytes, string UploadedBy, DateTimeOffset UploadedAt,
    ExtractionStatus Status, JobDto Job, string? Error, string? VoteId, SheetHeader? Header,
    IReadOnlyList<SheetRow> Rows, SheetTotals? Counted, SheetTotals? Printed, IReadOnlyList<SheetIssue> Issues, bool CanApprove);
