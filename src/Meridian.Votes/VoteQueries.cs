using System.ComponentModel.DataAnnotations;
using Meridian.Platform.Http;

namespace Meridian.Votes;

/// <summary>Query parameters of vote search, validated before the search runs.</summary>
public sealed class VoteSearchQuery : PagingQuery, IValidatableObject
{
    public string? Q { get; set; }
    public int? Session { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? NoPosition { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (From is { } from && To is { } to && from > to)
            yield return new ValidationResult("from must not be after to.", [nameof(From)]);
    }
}

/// <summary>Query parameters of the vote timeline.</summary>
public sealed class TimelineQuery
{
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "before must be positive.")]
    public long? Before { get; set; }

    [Range(1, PagingQuery.MaxPageSize, ErrorMessage = "pageSize must be between 1 and 200.")]
    public int? PageSize { get; set; }

    public int EffectivePageSize => PageSize ?? 50;
}
