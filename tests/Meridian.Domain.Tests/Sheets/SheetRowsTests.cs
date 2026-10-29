using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;

namespace Meridian.Domain.Tests.Sheets;

/// <summary>PDF-05: a checked sheet reads as a file's rows, and the CSV classifier judges them like any other.</summary>
public sealed class SheetRowsTests
{
    private static MappedSheet Sheet() =>
        SheetMapper.Map(SheetFixture.Build([Ballot.Y, Ballot.N, Ballot.A, Ballot.X], new SheetTotals(1, 1, 1, 1)));

    private static SheetCheck Checked(MappedSheet sheet) => SheetValidator.Check(sheet, "MA_2011_042.pdf", SheetFixture.Members(4));

    [Fact]
    public void Each_grid_row_becomes_the_export_row_a_csv_would_have_carried_in_sheet_order()
    {
        var sheet = Sheet();

        var rows = SheetRows.ToExportRows(sheet, Checked(sheet).Details!);

        Assert.Equal([1, 2, 3, 4], rows.Select(r => r.LineNo));
        Assert.Equal(
            [("MA/2011/042", "66", "2011-10-04", "Protection of Cultural Heritage", "AAA", "Y"), ("MA/2011/042", "66", "2011-10-04", "Protection of Cultural Heritage", "AAD", "X")],
            new[] { rows[0], rows[3] }.Select(r => (r.Fields!.AssemblyVoteId, r.Fields.Session, r.Fields.VoteDate, r.Fields.Title, r.Fields.CountryCode, r.Fields.Ballot)));
    }

    /// <summary>What the importer would hold as approved after the first sheet: every ballot, one revision each.</summary>
    private static ApprovedVote ApprovedAfter(MappedSheet sheet) => new(
        new Approved<VoteDetails>(1, Checked(sheet).Details!),
        sheet.Rows.ToDictionary(r => r.Code, r => new Approved<Ballot>(100 + r.Position, r.Choice!.Value)),
        []);

    [Fact]
    public void The_importer_s_own_classifier_calls_a_first_sheet_new_and_the_same_sheet_again_unchanged()
    {
        var members = SheetFixture.Members(4);
        var first = Sheet();

        var firstVerdict = Classifier.Classify(SheetRows.ToExportRows(first, Checked(first).Details!), ApprovedVote.Nothing, members);
        var again = Classifier.Classify(SheetRows.ToExportRows(first, Checked(first).Details!), ApprovedAfter(first), members);

        Assert.All(firstVerdict.Rows, r => Assert.Equal(Outcome.New, r.Outcome));
        Assert.All(again.Rows, r => Assert.Equal(Outcome.Unchanged, r.Outcome));
    }

    [Fact]
    public void A_corrigendum_row_that_changes_an_approved_ballot_is_a_conflict_and_the_rest_are_unchanged()
    {
        var members = SheetFixture.Members(4);
        var original = Sheet();
        var corrigendum = SheetMapper.Map(SheetFixture.Build([Ballot.Y, Ballot.N, Ballot.Y, Ballot.X], new SheetTotals(2, 1, 0, 1)));

        var verdict = Classifier.Classify(SheetRows.ToExportRows(corrigendum, Checked(corrigendum).Details!), ApprovedAfter(original), members);

        Assert.Equal([Outcome.Unchanged, Outcome.Unchanged, Outcome.Conflict, Outcome.Unchanged], verdict.Rows.Select(r => r.Outcome));
    }
}
