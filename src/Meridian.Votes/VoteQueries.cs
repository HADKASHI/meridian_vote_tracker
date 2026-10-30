using System.ComponentModel.DataAnnotations;
using Meridian.Platform.Http;

namespace Meridian.Votes;

/// <summary>Query parameters of vote search, validated before the search runs.</summary>
public sealed class VoteSearchQuery : PagingQuery, IValidatableObject
{
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "q")]
    public string? Q { get; set; }
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "session")]
    public int? Session { get; set; }
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "from")]
    public DateOnly? From { get; set; }
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "to")]
    public DateOnly? To { get; set; }
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "noPosition")]
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
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "before")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "before must be positive.")]
    public long? Before { get; set; }

    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "pageSize")]
    [Range(1, PagingQuery.MaxPageSize, ErrorMessage = "pageSize must be between 1 and 200.")]
    public int? PageSize { get; set; }

    public int EffectivePageSize => PageSize ?? 50;
}
