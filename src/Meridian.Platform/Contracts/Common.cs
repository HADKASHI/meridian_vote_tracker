using Meridian.Domain;

namespace Meridian.Platform.Contracts;

// Shapes several feature modules share. Each module's own DTOs live in the module.

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>A vote as other resources point at it: the internal id for links, the assembly id for display.</summary>
public sealed record VoteRefDto(long VoteId, string AssemblyVoteId);

public enum OriginKind { ImportRow, Decision }

/// <summary>Where an approved value came from: the file row that brought it, or the decision that approved it.</summary>
public sealed record OriginDto(
    OriginKind Kind,
    long? ImportId,
    string? FileName,
    int? LineNo,
    long? ImportRowId,
    string? UploadedBy,
    DateTimeOffset? UploadedAt,
    long? DecisionId,
    long? ConflictId,
    string? DecidedBy,
    DateTimeOffset? DecidedAt);

/// <summary>A vote's position. The revision id is its version for a conditional save.</summary>
public sealed record PositionDto(long RevisionId, Stance Stance, short Weight, string SetBy, DateTimeOffset SetAt);

/// <summary>Row counts per outcome, always queried from the row records.</summary>
public sealed record OutcomeCountsDto(int New, int Unchanged, int Duplicate, int Invalid, int Contradiction, int Conflict);
