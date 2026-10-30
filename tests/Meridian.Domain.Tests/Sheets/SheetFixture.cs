using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;

namespace Meridian.Domain.Tests.Sheets;

/// <summary>
/// Synthetic reader output shaped like the Assembly's scanned sheets (a header, a code / state / YES /
/// NO / ABST. grid in two side-by-side columns per page, a footer of recorded totals). No scanned
/// pixels and no client data: codes are generated, the figures are the 2011 sheet's printed totals.
/// </summary>
internal static class SheetFixture
{
    public static readonly DateOnly VoteDate = new(2011, 10, 4);

    /// <summary>Codes AAA, AAB, AAC, …: far more than any sheet needs and never a real country.</summary>
    public static string Code(int index) =>
        $"{(char)('A' + index / 676 % 26)}{(char)('A' + index / 26 % 26)}{(char)('A' + index % 26)}";

    public static MemberStates Members(int count = 148, DateOnly? from = null) => new(
        Enumerable.Range(0, count).ToDictionary(
            i => Code(i), i => (IReadOnlyList<MembershipSpan>)[new MembershipSpan(from ?? new DateOnly(1990, 1, 1), null)]));

    /// <summary>The 2011/042 sheet's recorded totals: 70 + 55 + 7 + 16 = 148.</summary>
    public static readonly SheetTotals Printed2011 = new(70, 55, 7, 16);

    /// <summary>One entry per row: the ballot the sheet shows, in member order.</summary>
    public static List<Ballot> Choices(SheetTotals totals) =>
        [.. Enumerable.Repeat(Ballot.Y, totals.Yes), .. Enumerable.Repeat(Ballot.N, totals.No),
         .. Enumerable.Repeat(Ballot.A, totals.Abstentions), .. Enumerable.Repeat(Ballot.X, totals.NotVoting)];

    public static SheetRead Build(IReadOnlyList<Ballot> choices, SheetTotals? printed, string voteId = "MA/2011/042",
        string session = "66", string date = "4 October 2011", string title = "Protection of Cultural Heritage",
        Action<int, SheetCell[]>? tweak = null, int rowsPerPage = 90)
    {
        var pages = new List<SheetPage>();
        for (var page = 0; page * rowsPerPage < choices.Count; page++)
        {
            var left = new List<IReadOnlyList<SheetCell>> { HeaderRow };
            var right = new List<IReadOnlyList<SheetCell>> { HeaderRow };
            var slice = Enumerable.Range(page * rowsPerPage, Math.Min(rowsPerPage, choices.Count - page * rowsPerPage)).ToList();
            for (var n = 0; n < slice.Count; n++)
            {
                var i = slice[n];
                var cells = RowCells(i, choices[i]);
                tweak?.Invoke(i, cells);
                (n < (slice.Count + 1) / 2 ? left : right).Add(cells);
            }
            var lines = new List<string>
            {
                "MERIDIAN ASSEMBLY", "RECORD OF VOTE", "Vote No.:", voteId, "Session:", session,
                "Date of vote:", date, "Resolution:", title, $"Page {page + 1} of {(choices.Count + rowsPerPage - 1) / rowsPerPage}",
            };
            if (printed is { } p && (page + 1) * rowsPerPage >= choices.Count)
                lines.Add($"Recorded totals: Yes: {p.Yes} No: {p.No} Abstentions: {p.Abstentions} Not voting: {p.NotVoting}");
            lines.Add("Certified correct:");
            pages.Add(new SheetPage(lines, [new SheetTable(left), new SheetTable(right)]));
        }
        return new SheetRead(pages);
    }

    private static readonly SheetCell[] HeaderRow =
        [new("Code", .99), new("Member state", .99), new("YES", .99), new("NO", .99), new("ABST.", .99)];

    public static SheetCell[] RowCells(int index, Ballot choice) =>
    [
        new(Code(index), .99), new($"State {Code(index)}", .98),
        new(choice == Ballot.Y ? "X" : "", .96), new(choice == Ballot.N ? "X" : "", .96), new(choice == Ballot.A ? "X" : "", .96),
    ];
}
