using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;

namespace Meridian.Imports;

public sealed record ImportCreatedDto(long ImportId, long JobId);

public enum LineEndings { Lf, Crlf, Mixed }

/// <summary>Parse-time facts. An import is rejected when <see cref="RejectedReason"/> is set, and then has no row records.</summary>
public sealed record ImportParseDto(
    IReadOnlyList<string> Header, bool HasBom, LineEndings LineEndings, int DataRows,
    DateTimeOffset? MaxPublishedAt, string? RejectedReason);

public enum ImportWarningKind { ExtraColumns, IdenticalFile, OlderPublishedAt, IncompleteVote }

/// <param name="IdenticalToImportId">For <c>identical_file</c>: the earlier import with the same SHA-256.</param>
/// <param name="Columns">For <c>extra_columns</c>: the columns ignored.</param>
/// <param name="MissingStateCodes">For <c>incomplete_vote</c>: members on the vote date with no approved country vote.</param>
/// <param name="NewestPublishedAt">For <c>older_published_at</c>: the newest <c>published_at</c> already imported.</param>
public sealed record ImportWarningDto(
    long Id, ImportWarningKind Kind, string Message, VoteRefDto? Vote,
    long? IdenticalToImportId, IReadOnlyList<string>? Columns, IReadOnlyList<string>? MissingStateCodes,
    DateTimeOffset? NewestPublishedAt);

/// <summary>A conflict this import opened, with how many of its rows link to it.</summary>
public sealed record ImportConflictDto(long ConflictId, VoteRefDto Vote, ConflictSubject Subject, string? StateCode, int Rows);

public sealed record ImportSummaryDto(
    long Id, string FileName, string UploadedBy, DateTimeOffset UploadedAt, long SizeBytes,
    JobDto Job, string? RejectedReason, OutcomeCountsDto Counts);

/// <summary>The import report: the file's evidence, its job, counts per outcome, warnings and the conflicts it opened.</summary>
public sealed record ImportDto(
    long Id, string FileName, string Sha256, long SizeBytes, string UploadedBy, DateTimeOffset UploadedAt,
    JobDto Job, ImportParseDto? Parse, OutcomeCountsDto Counts,
    IReadOnlyList<ImportWarningDto> Warnings, IReadOnlyList<ImportConflictDto> ConflictsOpened);

/// <summary>The verdict on one line of the file.</summary>
public sealed record ImportRowDto(
    long Id, int LineNo, string RawText, Outcome Outcome, string? ReasonCode, string? ReasonMessage,
    IReadOnlyList<string> Normalisations, VoteRefDto? Vote, string? StateCode,
    long? CountryVoteRevisionId, long? DetailsRevisionId, IReadOnlyList<long> ConflictIds);

public sealed record ImportVoteCountsDto(VoteRefDto Vote, OutcomeCountsDto Counts);
