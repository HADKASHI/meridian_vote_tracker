using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Meridian.Domain.Imports;

/// <summary>A row after map ticket 08 rule 2, with the first invalid reason found, if any.</summary>
public sealed record NormalisedRow(
    RowValues Values, IReadOnlyList<string> Normalisations, ReasonCode? Invalid, string? Message,
    VoteDetails? Details, Ballot? Ballot);

/// <summary>
/// Map ticket 08 rule 2 and the invalid checks of rules 3 and 12. Only documented, mechanical
/// changes are made, each recorded: trimming, Unicode NFC, invisible characters removed,
/// codes and single-letter ballots upper-cased, day-first dates rewritten as ISO. Ballot
/// words, empty ballots, unknown codes and ambiguous dates are invalid, never guessed.
/// </summary>
public static partial class RowNormaliser
{
    public static NormalisedRow Normalise(ExportRow row, MemberStates members)
    {
        var notes = new List<string>();
        if (row.Fields is not { } f)
            return new NormalisedRow(Empty, notes, ReasonCode.FieldCount,
                $"Wrong number of fields: {row.FieldCount} where the header has {row.HeaderFieldCount}.", null, null);

        var code = Clean(ExportParser.CountryCode, f.CountryCode, notes);
        var codeUpper = code.ToUpperInvariant();
        if (codeUpper != code) notes.Add($"{ExportParser.CountryCode}: upper-cased");
        var ballotText = Clean(ExportParser.Ballot, f.Ballot, notes);
        if (ballotText.Length == 1 && ballotText != ballotText.ToUpperInvariant())
        {
            ballotText = ballotText.ToUpperInvariant();
            notes.Add($"{ExportParser.Ballot}: upper-cased");
        }
        var voteId = Clean(ExportParser.AssemblyVoteId, f.AssemblyVoteId, notes);
        var sessionText = Clean(ExportParser.Session, f.Session, notes);
        var dateText = Clean(ExportParser.VoteDate, f.VoteDate, notes);
        var date = ParseDate(dateText);
        if (date is { } d && dateText != d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            notes.Add($"{ExportParser.VoteDate}: {dateText} read as {d:yyyy-MM-dd}");
        var title = Clean(ExportParser.Title, f.Title, notes);
        var name = f.CountryName is null ? null : Clean(ExportParser.CountryName, f.CountryName, notes);
        var publishedText = f.PublishedAt is null ? null : Clean(ExportParser.PublishedAt, f.PublishedAt, notes);

        int? session = SessionPattern().IsMatch(sessionText) && int.TryParse(sessionText, CultureInfo.InvariantCulture, out var s) && s > 0
            ? s
            : null;
        Ballot? ballot = ballotText is "Y" or "N" or "A" or "X" ? Enum.Parse<Ballot>(ballotText) : null;

        var values = new RowValues(voteId.Length == 0 ? null : voteId, codeUpper.Length == 0 ? null : codeUpper,
            name, ballotText.Length == 0 ? null : ballotText, ballot, session, date, title.Length == 0 ? null : title,
            ExportParser.ParsePublishedAt(publishedText));

        var missing = new[]
            {
                (ExportParser.AssemblyVoteId, voteId), (ExportParser.Session, sessionText), (ExportParser.VoteDate, dateText),
                (ExportParser.Title, title), (ExportParser.CountryCode, code),
            }
            .Where(x => x.Item2.Length == 0).Select(x => x.Item1).ToList();
        (ReasonCode, string)? invalid =
            missing.Count > 0 ? (ReasonCode.MissingField, $"Missing {string.Join(", ", missing)}.")
            : !members.IsKnown(codeUpper) ? (ReasonCode.UnknownCountryCode, $"Unknown country code {codeUpper}.")
            : ballotText.Length == 0 ? (ReasonCode.EmptyBallot, "Empty ballot: it is never read as absent (X).")
            : ballot is null ? (ReasonCode.InvalidBallot, $"Invalid ballot {ballotText}: only Y, N, A or X.")
            : date is null ? (ReasonCode.InvalidDate, $"Invalid date {dateText}: not one of the accepted day-first formats.")
            : session is null ? (ReasonCode.InvalidSession, $"Invalid session {sessionText}: not a positive integer.")
            : !members.IsMemberOn(codeUpper, date.Value) ? (ReasonCode.NotMemberOnDate, $"{codeUpper} was not a member on {date:yyyy-MM-dd}.")
            : null;

        return invalid is var (reason, message)
            ? new NormalisedRow(values, notes, reason, message, null, null)
            : new NormalisedRow(values, notes, null, null, new VoteDetails(session!.Value, date!.Value, title), ballot);
    }

    /// <summary>The row's vote id as grouped and stored, or null when the row can't name a vote.</summary>
    public static string? VoteKey(ExportRow row) =>
        row.Fields is { } f && Clean(f.AssemblyVoteId) is { Length: > 0 } id ? id : null;

    /// <summary>
    /// The comparison key (rule 2) for duplicates, contradictions, unchanged and conflicts:
    /// the stored value case-folded, whitespace runs collapsed, typographic quotes and dashes
    /// unified. Accents are kept.
    /// </summary>
    public static string ComparisonKey(string stored)
    {
        var key = new StringBuilder(stored.Length);
        foreach (var c in stored.ToLowerInvariant())
            key.Append(c switch
            {
                '‘' or '’' or '‚' or '‛' or '′' => '\'',
                '“' or '”' or '„' or '‟' or '″' or '«' or '»' => '"',
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
                _ => c,
            });
        return Whitespace().Replace(key.ToString(), " ").Trim();
    }

    private static readonly RowValues Empty = new(null, null, null, null, null, null, null, null, null);

    /// <summary>Removes invisible characters (NBSP becomes a space), applies NFC, trims; records each change.</summary>
    private static string Clean(string column, string value, List<string> notes)
    {
        var visible = RemoveInvisible(value);
        if (visible != value) notes.Add($"{column}: invisible characters removed");
        var nfc = visible.Normalize(NormalizationForm.FormC);
        if (nfc != visible) notes.Add($"{column}: Unicode NFC");
        var trimmed = nfc.Trim();
        if (trimmed != nfc) notes.Add($"{column}: trimmed");
        return trimmed;
    }

    private static string Clean(string value) => RemoveInvisible(value).Normalize(NormalizationForm.FormC).Trim();

    private static string RemoveInvisible(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == ' ') result.Append(' ');
            else if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format) continue;
            else result.Append(c);
        }
        return result.Length == value.Length && result.ToString() == value ? value : result.ToString();
    }

    private static readonly string[] Months =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    /// <summary>
    /// <c>yyyy-mm-dd</c>, <c>yyyy/mm/dd</c>, <c>dd-mm-yyyy</c>, <c>dd/mm/yyyy</c>, <c>dd.mm.yyyy</c> (leading zeros
    /// optional) and <c>14 March 2007</c>. Numeric dates are day-first; anything else is invalid.
    /// </summary>
    private static DateOnly? ParseDate(string text)
    {
        int year, month, day;
        if (YearFirst().Match(text) is { Success: true } ymd)
            (year, month, day) = (Int(ymd, "y"), Int(ymd, "m"), Int(ymd, "d"));
        else if (DayFirst().Match(text) is { Success: true } dmy)
            (year, month, day) = (Int(dmy, "y"), Int(dmy, "m"), Int(dmy, "d"));
        else if (LongForm().Match(text) is { Success: true } longForm
                 && Array.IndexOf(Months, longForm.Groups["month"].Value.ToLowerInvariant()) is var m and >= 0)
            (year, month, day) = (Int(longForm, "y"), m + 1, Int(longForm, "d"));
        else return null;

        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    private static int Int(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<y>\d{4})(?<sep>[-/])(?<m>\d{1,2})\k<sep>(?<d>\d{1,2})$")]
    private static partial Regex YearFirst();

    [GeneratedRegex(@"^(?<d>\d{1,2})(?<sep>[-/.])(?<m>\d{1,2})\k<sep>(?<y>\d{4})$")]
    private static partial Regex DayFirst();

    [GeneratedRegex(@"^(?<d>\d{1,2}) (?<month>[A-Za-z]+) (?<y>\d{4})$")]
    private static partial Regex LongForm();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex SessionPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
