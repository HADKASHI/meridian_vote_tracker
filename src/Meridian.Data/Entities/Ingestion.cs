using Meridian.Domain;

namespace Meridian.Data.Entities;

public sealed class Import
{
    public long Id { get; init; }
    public required string FileName { get; init; }
    public required string BlobName { get; init; }
    public required string Sha256 { get; init; }
    public long SizeBytes { get; init; }
    public required string UploadedBy { get; init; }
    /// <summary>The upload time.</summary>
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>Parse-time facts, one per import. Rejected when <see cref="RejectedReason"/> is set.</summary>
public sealed class ImportParse
{
    public long ImportId { get; init; }
    /// <summary>JSON array of the columns as found.</summary>
    public required string Header { get; init; }
    public bool HasBom { get; init; }
    /// <summary><c>lf</c>, <c>crlf</c> or <c>mixed</c>.</summary>
    public required string LineEndings { get; init; }
    public int DataRows { get; init; }
    public DateTimeOffset? MaxPublishedAt { get; init; }
    public string? RejectedReason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>The verdict on one line of an imported file, fixed at the moment of import.</summary>
public sealed class ImportRow
{
    public long Id { get; init; }
    public long ImportId { get; init; }
    public int LineNo { get; init; }
    public required string RawText { get; init; }
    public long? VoteId { get; init; }
    /// <summary>Plain text with no foreign key: unknown codes are stored too.</summary>
    public string? StateCode { get; init; }
    public string? CountryName { get; init; }
    public string? Ballot { get; init; }
    public int? Session { get; init; }
    public DateOnly? VoteDate { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public Outcome Outcome { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonMessage { get; init; }
    /// <summary>JSON list of the normalisations applied.</summary>
    public string? Normalisations { get; init; }
    /// <summary>The country vote revision this row created or matched.</summary>
    public long? CountryVoteRevisionId { get; init; }
    /// <summary>The vote details revision this row created or matched.</summary>
    public long? DetailsRevisionId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class ImportWarning
{
    public long Id { get; init; }
    public long ImportId { get; init; }
    public required string Kind { get; init; }
    public long? VoteId { get; init; }
    public required string Detail { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
