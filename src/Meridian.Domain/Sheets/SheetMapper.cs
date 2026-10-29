using System.Text.RegularExpressions;

namespace Meridian.Domain.Sheets;

/// <summary>
/// Reads what a reader returned as a vote: the printed header, one row per grid row (the printed
/// Code column names the state; the column holding a mark gives the ballot; a blank row is absent)
/// and the recorded totals. Pages merge in order. It decides nothing about validity; that is SheetValidator.
/// </summary>
public static partial class SheetMapper
{
    private const int GridColumns = 5; // Code, Member state, YES, NO, ABST.

    public static MappedSheet Map(SheetRead read)
    {
        var lines = read.Pages.SelectMany(p => p.Lines).ToList();
        var header = new SheetHeader(Field(lines, "Vote No"), Field(lines, "Session"), Field(lines, "Date of vote"), Field(lines, "Resolution"));

        var rows = new List<SheetRow>();
        for (var page = 0; page < read.Pages.Count; page++)
            foreach (var table in read.Pages[page].Tables)
                foreach (var cells in table.Rows)
                    foreach (var grid in Chunks(cells))
                        if (IsDataRow(grid))
                            rows.Add(ToRow(grid, page + 1, rows.Count + 1));

        return new MappedSheet(header, rows, PrintedTotals(lines));
    }

    /// <summary>A row of ten cells is two grid rows side by side; anything else is read as it comes.</summary>
    private static IEnumerable<IReadOnlyList<SheetCell>> Chunks(IReadOnlyList<SheetCell> cells)
    {
        if (cells.Count >= GridColumns * 2 && cells.Count % GridColumns == 0)
            for (var at = 0; at < cells.Count; at += GridColumns) yield return cells.Skip(at).Take(GridColumns).ToList();
        else
            yield return cells;
    }

    private static bool IsDataRow(IReadOnlyList<SheetCell> cells) =>
        cells.Count >= GridColumns
        && cells[0].Text.Trim() is { Length: > 0 } code
        && !code.Equals("Code", StringComparison.OrdinalIgnoreCase);

    private static SheetRow ToRow(IReadOnlyList<SheetCell> cells, int page, int position)
    {
        var marks = new[] { (Ballot.Y, cells[2]), (Ballot.N, cells[3]), (Ballot.A, cells[4]) }
            .Where(m => IsMark(m.Item2.Text)).ToList();
        var confidence = new[] { cells[0].Confidence }.Concat(marks.Select(m => m.Item2.Confidence)).Min();
        var choice = marks.Count switch { 0 => Ballot.X, 1 => marks[0].Item1, _ => (Ballot?)null };
        return new SheetRow(cells[0].Text.Trim().ToUpperInvariant(), cells[1].Text.Trim(), choice, marks.Count > 1, confidence, page, position);
    }

    /// <summary>Any text in a vote column is a mark, except the reader's own "nothing here" markers.</summary>
    private static bool IsMark(string text) =>
        text.Trim() is { Length: > 0 } t && !t.Equals(":unselected:", StringComparison.OrdinalIgnoreCase);

    /// <summary>A label ("Date of vote:") and its value, on the same line or on the next one.</summary>
    private static string? Field(IReadOnlyList<string> lines, string label)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith(label, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = line[label.Length..].TrimStart('.', ' ', ':').Trim();
            if (rest.Length > 0) return rest;
            if (i + 1 < lines.Count && lines[i + 1].Trim() is { Length: > 0 } next) return next;
        }
        return null;
    }

    /// <summary>The printed totals, or null when the footer is missing or a number in it is not a plausible count.</summary>
    private static SheetTotals? PrintedTotals(IReadOnlyList<string> lines) =>
        TotalsPattern().Match(string.Join(' ', lines)) is { Success: true } m
            && int.TryParse(m.Groups["y"].Value, out var yes) && int.TryParse(m.Groups["n"].Value, out var no)
            && int.TryParse(m.Groups["a"].Value, out var abstentions) && int.TryParse(m.Groups["x"].Value, out var notVoting)
                ? new SheetTotals(yes, no, abstentions, notVoting)
                : null;

    [GeneratedRegex(@"Yes\s*:?\s*(?<y>\d+)\s+No\s*:?\s*(?<n>\d+)\s+Abstentions?\s*:?\s*(?<a>\d+)\s+Not\s+voting\s*:?\s*(?<x>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TotalsPattern();
}
