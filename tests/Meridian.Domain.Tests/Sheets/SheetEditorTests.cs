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

    private static readonly IReadOnlySet<string> Flagged = new HashSet<string> { "AAB" };

    [Fact]
    public void A_flagged_row_is_removed_and_the_edit_records_the_ballot_it_had_while_the_others_keep_their_positions()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(2, null, null, true)], null, null, Flagged);

        Assert.Empty(result.Errors);
        Assert.Equal([1, 3], result.Sheet.Rows.Select(r => r.Position));
        Assert.Equal(new SheetEdit("removed", 2, "AAB", "two marks", null), Assert.Single(result.Edits));
    }

    [Fact]
    public void A_row_without_an_issue_cannot_be_removed()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(1, null, null, true)], null, null, Flagged);

        Assert.Equal(["rows[0].remove"], result.Errors.Keys);
        Assert.Equal(3, result.Sheet.Rows.Count);
    }

    [Fact]
    public void Nothing_can_be_removed_when_the_caller_names_no_flagged_rows()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(2, null, null, true)], null, null);

        Assert.Equal(["rows[0].remove"], result.Errors.Keys);
    }

    [Fact]
    public void A_row_to_remove_cannot_also_be_changed()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(2, "ZZZ", null, true)], null, null, Flagged);

        Assert.Equal(["rows[0].remove"], result.Errors.Keys);
    }

    [Fact]
    public void A_row_edited_after_a_removal_is_still_found_by_its_position_and_counts_as_certain()
    {
        var result = SheetEditor.Apply(Sheet(), [new RowEdit(2, null, null, true), new RowEdit(3, null, Ballot.N)], null, null, Flagged);

        Assert.Empty(result.Errors);
        Assert.Equal((3, Ballot.N, 1.0), (result.Sheet.Rows[1].Position, result.Sheet.Rows[1].Choice!.Value, result.Sheet.Rows[1].Confidence));
    }

    // Sheet() counts 1 yes, 0 no, 1 abstention (its middle row has two marks); the printed totals say otherwise.
    private static MappedSheet Disagreeing() => Sheet() with { Printed = new SheetTotals(2, 1, 1, 0) };

    [Fact]
    public void Accepting_the_totals_keeps_the_counts_and_records_both_sets()
    {
        var result = SheetEditor.Apply(Disagreeing(), null, null, null, null, acceptTotals: true);

        Assert.Empty(result.Errors);
        Assert.Equal(new SheetTotals(1, 0, 1, 0), result.Sheet.AcceptedCounts);
        Assert.Equal(new SheetEdit("totalsAccepted", null, null, "Yes 2, No 1, Abstentions 1, Not voting 0", "Yes 1, No 0, Abstentions 1, Not voting 0"),
            Assert.Single(result.Edits));
    }

    [Fact]
    public void There_is_nothing_to_accept_when_the_counts_match_or_no_totals_are_printed()
    {
        var matching = SheetEditor.Apply(Sheet() with { Printed = new SheetTotals(1, 0, 1, 0) }, null, null, null, null, acceptTotals: true);
        var none = SheetEditor.Apply(Sheet() with { Printed = null }, null, null, null, null, acceptTotals: true);

        Assert.Equal(["acceptTotals"], matching.Errors.Keys);
        Assert.Equal(["acceptTotals"], none.Errors.Keys);
    }

    [Fact]
    public void Entering_other_printed_totals_voids_an_earlier_acceptance()
    {
        var accepted = Disagreeing() with { AcceptedCounts = new SheetTotals(1, 0, 1, 0) };

        var result = SheetEditor.Apply(accepted, null, null, new SheetTotals(3, 0, 0, 0));

        Assert.Null(result.Sheet.AcceptedCounts);
    }

    [Fact]
    public void Changing_a_row_after_accepting_means_the_counts_no_longer_match_what_was_accepted()
    {
        var accepted = Disagreeing() with { AcceptedCounts = new SheetTotals(1, 0, 1, 0) };

        var result = SheetEditor.Apply(accepted, [new RowEdit(1, null, Ballot.N)], null, null);

        Assert.NotEqual(result.Sheet.AcceptedCounts, SheetValidator.Count(result.Sheet.Rows));
    }
}
