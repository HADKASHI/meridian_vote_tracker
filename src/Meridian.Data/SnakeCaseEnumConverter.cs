using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Meridian.Data;

/// <summary>Stores an enum as its snake_case name (<c>NonMember</c> → <c>non_member</c>), the word the glossary and the API use.</summary>
public sealed partial class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToName(value), name => FromName(name))
    where TEnum : struct, Enum
{
    public static TEnum FromName(string name) => Enum.Parse<TEnum>(name.Replace("_", ""), ignoreCase: true);

    public static string ToName(TEnum value) => WordBoundary().Replace(value.ToString(), "$1_$2").ToLowerInvariant();

    /// <summary>The SQL list of every stored name, for CHECK constraints.</summary>
    public static string SqlList() => string.Join(", ", Enum.GetValues<TEnum>().Select(v => $"'{ToName(v)}'"));

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
