using System.ComponentModel.DataAnnotations;

namespace Meridian.Platform.Http;

/// <summary>
/// Page and pageSize query parameters, validated at the perimeter (400) instead of clamped.
/// <see cref="MaxPage"/> keeps <c>(page - 1) * pageSize</c> within int range.
/// </summary>
public class PagingQuery
{
    public const int MaxPage = 1_000_000;
    public const int MaxPageSize = 200;
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "page")]
    [Range(1, MaxPage, ErrorMessage = "page must be between 1 and 1000000.")]
    public int? Page { get; set; }

    public int EffectivePage => Page ?? 1;
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "pageSize")]
    [Range(1, MaxPageSize, ErrorMessage = "pageSize must be between 1 and 200.")]
    public int? PageSize { get; set; }

    public int EffectivePageSize => PageSize ?? 50;
}
