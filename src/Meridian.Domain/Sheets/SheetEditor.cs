using System.Globalization;

namespace Meridian.Domain.Sheets;

/// <summary>A change to one grid row, found by its position on the sheet (1 for the first). Null leaves a field as it is.</summary>
public sealed record RowEdit(int Position, string? Code, Ballot? Choice);

/// <summary>A change to the header fields as printed. Null leaves a field as it is.</summary>
public sealed record HeaderEdit(string? VoteId, string? Session, string? Date, string? Title);

/// <summary>What one edit changed: a field, the row it was on, and the value before and after as text.</summary>
public sealed record SheetEdit(string Field, int? Position, string? Code, string? From, string? To);

/// <summary>The sheet after the edits, what changed, and what was wrong with the request (by JSON path).</summary>
public sealed record SheetEditResult(MappedSheet Sheet, IReadOnlyList<SheetEdit> Edits, IReadOnlyDictionary<string, string> Errors);

/// <summary>
/// An analyst's corrections to a sheet (PDF-04). Choosing a ballot settles a row that had two marks; a code is
/// upper-cased; the printed totals can be entered when the footer could not be read. Only real changes are
/// recorded, with the value they replaced, and nothing is checked here: SheetValidator checks the result. A row an
/// analyst changed counts as certain (confidence 1), because a person set it.
/// </summary>
public static class SheetEditor
{
    public static SheetEditResult Apply(MappedSheet sheet, IReadOnlyList<RowEdit>? rows, HeaderEdit? header, SheetTotals? printed)
    {
        var errors = new Dictionary<string, string>();
        var edits = new List<SheetEdit>();
        var current = sheet.Rows.ToList();

        for (var i = 0; i < (rows?.Count ?? 0); i++)
        {
            var edit = rows![i];
            var at = current.FindIndex(r => r.Position == edit.Position);
            if (at < 0)
            {
                errors[$"rows[{i}].position"] = $"The sheet has no row {edit.Position.ToString(CultureInfo.InvariantCulture)}.";
                continue;
            }
            var row = current[at];
            if (edit.Code is { } code)
            {
                var clean = code.Trim().ToUpperInvariant();
                if (clean.Length == 0) errors[$"rows[{i}].code"] = "A code can't be empty.";
                else if (clean != row.Code)
                {
                    edits.Add(new SheetEdit("code", row.Position, row.Code, row.Code, clean));
                    row = row with { Code = clean };
                }
            }
            if (edit.Choice is { } choice && (row.Ambiguous || row.Choice != choice))
            {
                edits.Add(new SheetEdit("choice", row.Position, row.Code, row.Ambiguous ? null : row.Choice?.ToString(), choice.ToString()));
                row = row with { Choice = choice, Ambiguous = false };
            }
            // A person has now set this row, so the reader's doubt no longer applies; what it read stays in the edit.
            current[at] = row == sheet.Rows[at] ? row : row with { Confidence = 1 };
        }

        var head = sheet.Header;
        if (header is not null)
        {
            head = Field(head, "voteId", header.VoteId, head.VoteId, v => head with { VoteId = v }, edits);
            head = Field(head, "session", header.Session, head.Session, v => head with { Session = v }, edits);
            head = Field(head, "date", header.Date, head.Date, v => head with { Date = v }, edits);
            head = Field(head, "title", header.Title, head.Title, v => head with { Title = v }, edits);
        }

        var printedNow = sheet.Printed;
        if (printed is not null && printed != sheet.Printed)
        {
            edits.Add(new SheetEdit("printed", null, null, sheet.Printed is { } was ? Describe(was) : null, Describe(printed)));
            printedNow = printed;
        }

        if (errors.Count == 0 && edits.Count == 0) errors["edits"] = "There is nothing to save: no field differs from the sheet.";
        return new SheetEditResult(new MappedSheet(head, current, printedNow), edits, errors);
    }

    private static SheetHeader Field(SheetHeader header, string field, string? wanted, string? was, Func<string, SheetHeader> set, List<SheetEdit> edits)
    {
        if (wanted is null || wanted.Trim() == was) return header;
        edits.Add(new SheetEdit(field, null, null, was, wanted.Trim()));
        return set(wanted.Trim());
    }

    private static string Describe(SheetTotals t) => $"Yes {t.Yes}, No {t.No}, Abstentions {t.Abstentions}, Not voting {t.NotVoting}";
}
