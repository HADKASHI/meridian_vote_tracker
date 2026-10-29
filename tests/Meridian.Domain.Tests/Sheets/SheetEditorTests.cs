using Meridian.Domain.Sheets;

namespace Meridian.Domain.Tests.Sheets;

/// <summary>PDF-04: an analyst's corrections, with the value each replaced.</summary>
public sealed class SheetEditorTests
{
    private static MappedSheet Sheet() => SheetMapper.Map(SheetFixture.Build(
        [Ballot.Y, Ballot.N, Ballot.A], SheetFixture.Printed2011, tweak: (i, cells) => { if (i == 1) cells[4] = new("X", .5); }));

    [Fact]
    public void Choosing_a_ballot_settles_a_row_with_two_marks_and_records_that_it_had_none()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(2, null, Ballot.N)], null, null);

        Assert.Empty(result.Errors);
        Assert.Equal((Ballot.N, false, 1.0), (result.Sheet.Rows[1].Choice!.Value, result.Sheet.Rows[1].Ambiguous, result.Sheet.Rows[1].Confidence));
        Assert.Equal(new SheetEdit("choice", 2, "AAB", null, "N"), Assert.Single(result.Edits));
    }

    [Fact]
    public void A_changed_choice_records_the_ballot_it_replaced()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(1, null, Ballot.A)], null, null);

        Assert.Equal(new SheetEdit("choice", 1, "AAA", "Y", "A"), Assert.Single(result.Edits));
    }

    [Fact]
    public void A_code_is_trimmed_and_upper_cased_and_records_the_old_one()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(3, " aac2 ", null)], null, null);

        Assert.Equal("AAC2", result.Sheet.Rows[2].Code);
        Assert.Equal(new SheetEdit("code", 3, "AAC", "AAC", "AAC2"), Assert.Single(result.Edits));
    }

    [Fact]
    public void Header_fields_and_printed_totals_are_edited_with_what_they_replaced()
    {
        var result = SheetEditor.Apply(Sheet(), null, new HeaderEdit(null, null, " 5 October 2011 ", null), new SheetTotals(1, 1, 1, 0));

        Assert.Equal("5 October 2011", result.Sheet.Header.Date);
        Assert.Equal(new SheetTotals(1, 1, 1, 0), result.Sheet.Printed);
        Assert.Equal(
            [("date", "4 October 2011", "5 October 2011"), ("printed", "Yes 70, No 55, Abstentions 7, Not voting 16", "Yes 1, No 1, Abstentions 1, Not voting 0")],
            result.Edits.Select(e => (e.Field, e.From, e.To)));
    }

    [Fact]
    public void Values_equal_to_what_the_sheet_already_holds_are_not_edits_and_an_empty_save_is_refused()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(1, "aaa", Ballot.Y)], new HeaderEdit(null, "66", null, null), SheetFixture.Printed2011);

        Assert.Empty(result.Edits);
        Assert.Contains("edits", result.Errors.Keys);
    }

    [Fact]
    public void A_row_that_does_not_exist_or_an_empty_code_is_an_error_keyed_by_its_json_path()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(1, "  ", null), new RowEdit(99, null, Ballot.Y)], null, null);

        Assert.Equal(["rows[0].code", "rows[1].position"], result.Errors.Keys.Order());
    }

    [Fact]
    public void Editing_does_not_change_the_sheet_it_was_given()
    {
        var sheet = Sheet();

        SheetEditor.Apply(sheet, [new RowEdit(1, "ZZZ", Ballot.N)], null, null);

        Assert.Equal(("AAA", Ballot.Y), (sheet.Rows[0].Code, sheet.Rows[0].Choice!.Value));
    }
}
