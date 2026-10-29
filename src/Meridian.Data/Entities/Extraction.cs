using Meridian.Domain;

namespace Meridian.Data.Entities;

/// <summary>
/// A scanned voting sheet and what was read from it, waiting for a person (ADR 0008). Mutable like a job:
/// it moves from extracting to a decision, and an analyst's edits change <see cref="Sheet"/> while
/// <see cref="Extracted"/> keeps what the reader said. Nothing here is approved data until it is approved.
/// </summary>
public sealed class Extraction
{
    public long Id { get; init; }
    public required string FileName { get; init; }
    public required string BlobName { get; init; }
    public required string Sha256 { get; init; }
    public long SizeBytes { get; init; }
    public required string UploadedBy { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public ExtractionStatus Status { get; set; }
    /// <summary>JSON of the mapped sheet exactly as read; never changed after the job writes it.</summary>
    public string? Extracted { get; set; }
    /// <summary>JSON of the mapped sheet with the analyst's edits applied.</summary>
    public string? Sheet { get; set; }
    /// <summary>JSON of the last check: issues, counted and printed totals, the vote it reads as.</summary>
    public string? CheckResult { get; set; }
    /// <summary>JSON list of the edits: what changed, from what, by whom and when.</summary>
    public string Edits { get; set; } = "[]";
    /// <summary>Why it failed, in words free of the sheet's content.</summary>
    public string? Error { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    /// <summary>The import an approval made from this sheet.</summary>
    public long? ImportId { get; set; }
}
