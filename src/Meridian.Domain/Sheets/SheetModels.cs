namespace Meridian.Domain.Sheets;

// What a reader (Azure Document Intelligence, or a fake in tests) returns for a scanned sheet.
// The reader says what it saw and how sure it is; SheetMapper decides what it means (ADR 0008).

/// <summary>One table cell as read: its text and the reader's confidence, 0 to 1.</summary>
public sealed record SheetCell(string Text, double Confidence);

/// <summary>A table as rows of cells. A side-by-side grid may arrive as two tables or as rows of ten cells.</summary>
public sealed record SheetTable(IReadOnlyList<IReadOnlyList<SheetCell>> Rows);

/// <summary>One scanned page: its text lines in reading order and its tables.</summary>
public sealed record SheetPage(IReadOnlyList<string> Lines, IReadOnlyList<SheetTable> Tables);

public sealed record SheetRead(IReadOnlyList<SheetPage> Pages);

/// <summary>The header fields exactly as printed (the date is "4 October 2011", not yet a date).</summary>
public sealed record SheetHeader(string? VoteId, string? Session, string? Date, string? Title);

/// <summary>The four totals a sheet prints, or counts: yes, no, abstentions, not voting.</summary>
public sealed record SheetTotals(int Yes, int No, int Abstentions, int NotVoting)
{
    public int Total => Yes + No + Abstentions + NotVoting;
}

/// <summary>
/// One grid row. <see cref="Choice"/> is null, and <see cref="Ambiguous"/> true, when more than one
/// column is marked: it is never guessed. A blank row is <see cref="Domain.Ballot.X"/> (did not vote).
/// </summary>
public sealed record SheetRow(string Code, string Name, Ballot? Choice, bool Ambiguous, double Confidence, int Page, int Position);

/// <summary>A sheet as the mapper reads it, before any checking: this is also what an analyst edits.</summary>
/// <param name="AcceptedCounts">
/// The counts an analyst knowingly accepted although they differ from the printed totals. It holds only while the rows
/// still count to exactly this, so any later change to a row, or to the printed totals, asks for the decision again.
/// </param>
public sealed record MappedSheet(SheetHeader Header, IReadOnlyList<SheetRow> Rows, SheetTotals? Printed, SheetTotals? AcceptedCounts = null);
