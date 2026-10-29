using System.Globalization;
using Meridian.Domain.Imports;

namespace Meridian.Domain.Sheets;

public enum SheetIssueSeverity { Blocking, Warning }

public enum SheetIssueKind
{
    Header, TotalsMissing, TotalsMismatch, UnknownCode, NotMemberOnDate, DuplicateCode, AmbiguousMark,
    LowConfidence, VoteIdMismatch, MissingMember, NoRows,
}

/// <summary>One thing wrong or doubtful on a sheet. Messages carry codes and counts, never the sheet's own text.</summary>
public sealed record SheetIssue(SheetIssueSeverity Severity, SheetIssueKind Kind, string Message, string? Code = null);

/// <summary>The outcome of checking a sheet: the vote it reads as, what was counted, and what blocks or warns.</summary>
public sealed record SheetCheck(
    string? VoteId, VoteDetails? Details, SheetTotals Counted, SheetTotals? Printed, IReadOnlyList<SheetIssue> Issues)
{
    public bool CanApprove => Issues.All(i => i.Severity != SheetIssueSeverity.Blocking);
}

/// <summary>
/// PDF-03 and PDF-04. Blocking: the header can't be read as a vote, no row could be read at all, the counts differ from the printed totals
/// (or none are printed), a code is unknown, wasn't a member on the vote date or appears twice, or a row has
/// two marks. Warnings: a low-confidence row, a header vote id that doesn't match the file name, a member on
/// the date with no row. Membership is the import's own <see cref="MemberStates"/>, the date reading its own
/// <see cref="RowNormaliser"/>. Pure, so an analyst's edit is checked again with the same call.
/// </summary>
public static class SheetValidator
{
    public const double LowConfidence = 0.7;

    public static SheetCheck Check(MappedSheet sheet, string fileName, MemberStates members)
    {
        var issues = new List<SheetIssue>();
        var details = ReadDetails(sheet.Header, issues);
        var counted = Count(sheet.Rows);

        if (sheet.Rows.Count == 0) Add(issues, SheetIssueKind.NoRows, "No rows could be read from the sheet.");
        CheckTotals(counted, sheet.Printed, issues);
        CheckRows(sheet.Rows, details?.VoteDate, members, issues);
        if (details is { } d) CheckMissingMembers(sheet.Rows, d.VoteDate, members, issues);
        CheckFileName(sheet.Header.VoteId, fileName, issues);

        return new SheetCheck(sheet.Header.VoteId, details, counted, sheet.Printed, issues);
    }

    public static SheetTotals Count(IEnumerable<SheetRow> rows)
    {
        var byChoice = rows.Where(r => r.Choice is not null).GroupBy(r => r.Choice!.Value).ToDictionary(g => g.Key, g => g.Count());
        int Of(Ballot b) => byChoice.GetValueOrDefault(b);
        return new SheetTotals(Of(Ballot.Y), Of(Ballot.N), Of(Ballot.A), Of(Ballot.X));
    }

    private static VoteDetails? ReadDetails(SheetHeader header, List<SheetIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(header.VoteId)) Add(issues, SheetIssueKind.Header, "The vote number is missing.");
        if (string.IsNullOrWhiteSpace(header.Title)) Add(issues, SheetIssueKind.Header, "The resolution is missing.");

        int? session = int.TryParse(header.Session?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : null;
        if (session is null) Add(issues, SheetIssueKind.Header, "The session is not a positive whole number.");

        var date = header.Date is { } text ? RowNormaliser.ParseDate(text.Trim()) : null;
        if (date is null) Add(issues, SheetIssueKind.Header, "The date of the vote can't be read as a date.");

        return session is { } sn && date is { } dt && !string.IsNullOrWhiteSpace(header.Title) && !string.IsNullOrWhiteSpace(header.VoteId)
            ? new VoteDetails(sn, dt, header.Title.Trim())
            : null;
    }

    private static void CheckTotals(SheetTotals counted, SheetTotals? printed, List<SheetIssue> issues)
    {
        if (printed is null)
        {
            Add(issues, SheetIssueKind.TotalsMissing, "The totals printed on the sheet could not be read, so the counts can't be checked.");
            return;
        }
        if (counted != printed)
            Add(issues, SheetIssueKind.TotalsMismatch,
                $"Counted {Describe(counted)} but the sheet prints {Describe(printed)}.");
    }

    private static string Describe(SheetTotals t) => $"Yes {t.Yes}, No {t.No}, Abstentions {t.Abstentions}, Not voting {t.NotVoting}";

    private static void CheckRows(IReadOnlyList<SheetRow> rows, DateOnly? date, MemberStates members, List<SheetIssue> issues)
    {
        foreach (var duplicate in rows.GroupBy(r => r.Code).Where(g => g.Count() > 1))
            Add(issues, SheetIssueKind.DuplicateCode, $"{duplicate.Key} appears {duplicate.Count()} times.", duplicate.Key);

        foreach (var row in rows)
        {
            if (!members.IsKnown(row.Code))
                Add(issues, SheetIssueKind.UnknownCode, $"{row.Code} is not a known member state code.", row.Code);
            else if (date is { } d && !members.IsMemberOn(row.Code, d))
                Add(issues, SheetIssueKind.NotMemberOnDate, $"{row.Code} was not a member on {d:yyyy-MM-dd}.", row.Code);

            if (row.Ambiguous || row.Choice is null)
                Add(issues, SheetIssueKind.AmbiguousMark, $"{row.Code} has more than one mark.", row.Code);
            if (row.Confidence < LowConfidence)
                Add(issues, SheetIssueKind.LowConfidence, $"{row.Code} was read with low confidence ({row.Confidence:0.00}).", row.Code);
        }
    }

    private static void CheckMissingMembers(IReadOnlyList<SheetRow> rows, DateOnly date, MemberStates members, List<SheetIssue> issues)
    {
        var present = rows.Select(r => r.Code).ToHashSet();
        foreach (var code in members.MembersOn(date).Where(c => !present.Contains(c)).Order(StringComparer.Ordinal))
            Add(issues, SheetIssueKind.MissingMember, $"{code} was a member on {date:yyyy-MM-dd} but has no row on the sheet.", code);
    }

    /// <summary>MA/2011/042 matches MA_2011_042.pdf and MA_2011_042_corrigendum.pdf, not MA_2011_043.pdf.</summary>
    private static void CheckFileName(string? voteId, string fileName, List<SheetIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(voteId)) return;
        var key = voteId.Trim().Replace('/', '_');
        var name = Path.GetFileNameWithoutExtension(fileName);
        var matches = name.Equals(key, StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith(key, StringComparison.OrdinalIgnoreCase) && !char.IsLetterOrDigit(name[key.Length]));
        if (!matches)
            Add(issues, SheetIssueKind.VoteIdMismatch, "The vote number on the sheet does not match the file name.");
    }

    private static void Add(List<SheetIssue> issues, SheetIssueKind kind, string message, string? code = null) =>
        issues.Add(new SheetIssue(kind is SheetIssueKind.LowConfidence or SheetIssueKind.VoteIdMismatch or SheetIssueKind.MissingMember
            ? SheetIssueSeverity.Warning : SheetIssueSeverity.Blocking, kind, message, code));
}
