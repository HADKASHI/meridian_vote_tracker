using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;

namespace Meridian.Domain.Tests.Sheets;

/// <summary>PDF-03 and PDF-04: what a reader returned for a voting sheet, read as a vote.</summary>
public sealed class SheetMapperTests
{
    private static MappedSheet Map(IReadOnlyList<Ballot> choices, SheetTotals? printed = null) =>
        SheetMapper.Map(SheetFixture.Build(choices, printed ?? SheetFixture.Printed2011));

    [Fact]
    public void A_sheet_gives_one_country_vote_per_row_by_the_printed_code_with_a_blank_row_as_absent()
    {
        var sheet = Map([Ballot.Y, Ballot.N, Ballot.A, Ballot.X]);

        Assert.Equal(
            [("AAA", Ballot.Y), ("AAB", Ballot.N), ("AAC", Ballot.A), ("AAD", Ballot.X)],
            sheet.Rows.Select(r => (r.Code, r.Choice!.Value)).ToArray());
    }

    [Fact]
    public void The_header_and_the_recorded_totals_are_read_as_printed()
    {
        var sheet = Map(SheetFixture.Choices(SheetFixture.Printed2011));

        Assert.Equal(new SheetHeader("MA/2011/042", "66", "4 October 2011", "Protection of Cultural Heritage"), sheet.Header);
        Assert.Equal(SheetFixture.Printed2011, sheet.Printed);
        Assert.Equal(148, sheet.Rows.Count);
    }

    [Fact]
    public void Pages_of_one_sheet_merge_into_one_vote_in_sheet_order()
    {
        var sheet = SheetMapper.Map(SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011, rowsPerPage: 90));

        Assert.Equal(148, sheet.Rows.Count);
        Assert.Equal(SheetFixture.Code(0), sheet.Rows[0].Code);
        Assert.Equal([1, 2], sheet.Rows.Select(r => r.Page).Distinct());
        Assert.Equal(Enumerable.Range(0, 148).Select(SheetFixture.Code), sheet.Rows.Select(r => r.Code));
    }

    [Fact]
    public void A_row_with_two_marks_has_no_choice_and_is_marked_ambiguous()
    {
        var read = SheetFixture.Build([Ballot.Y, Ballot.N], null, tweak: (i, cells) => { if (i == 1) cells[2] = new("X", .9); });

        var row = SheetMapper.Map(read).Rows[1];

        Assert.Null(row.Choice);
        Assert.True(row.Ambiguous);
    }

    [Fact]
    public void A_footer_number_too_big_to_be_a_count_is_an_unreadable_footer_not_a_crash()
    {
        var read = new SheetRead([new SheetPage(
            ["Recorded totals: Yes: 99999999999999999999 No: 5 Abstentions: 1 Not voting: 2"], [])]);

        var sheet = SheetMapper.Map(read);

        Assert.Null(sheet.Printed);
    }

    [Fact]
    public void A_row_is_only_as_sure_as_its_least_certain_cell()
    {
        var read = SheetFixture.Build([Ballot.Y], null, tweak: (_, cells) => cells[2] = new("X", .41));

        Assert.Equal(.41, SheetMapper.Map(read).Rows[0].Confidence);
    }
}
