using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;

namespace Meridian.Domain.Tests.Sheets;

/// <summary>PDF-03 and PDF-04: what blocks approving a sheet, and what only warns.</summary>
public sealed class SheetCheckTests
{
    private const string FileName = "MA_2011_042.pdf";

    private static SheetCheck Check(SheetRead read, MemberStates? members = null, string fileName = FileName) =>
        SheetValidator.Check(SheetMapper.Map(read), fileName, members ?? SheetFixture.Members());

    private static SheetRead Clean() => SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011);

    private static SheetIssue[] Blocking(SheetCheck check) => check.Issues.Where(i => i.Severity == SheetIssueSeverity.Blocking).ToArray();

    [Fact]
    public void A_sheet_whose_counts_equal_its_printed_totals_has_no_issues_and_is_read_as_a_vote()
    {
        var check = Check(Clean());

        Assert.Empty(check.Issues);
        Assert.True(check.CanApprove);
        Assert.Equal(SheetFixture.Printed2011, check.Counted);
        Assert.Equal(new VoteDetails(66, SheetFixture.VoteDate, "Protection of Cultural Heritage"), check.Details);
        Assert.Equal("MA/2011/042", check.VoteId);
    }

    [Fact]
    public void The_corrigendum_with_one_state_moved_from_yes_to_abstain_is_clean_against_its_own_totals()
    {
        var corrigendum = new SheetTotals(69, 55, 8, 16);

        var check = Check(SheetFixture.Build(SheetFixture.Choices(corrigendum), corrigendum));

        Assert.Empty(check.Issues);
        Assert.Equal(corrigendum, check.Counted);
    }

    [Fact]
    public void Counts_that_differ_from_a_printed_total_block_approval_and_say_both_figures()
    {
        // One mark read as a different column: the sheet prints 70 yes but the reader found 69.
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 0) { cells[2] = new("", .9); cells[4] = new("X", .9); } });

        var check = Check(read);

        var issue = Assert.Single(Blocking(check));
        Assert.Equal(SheetIssueKind.TotalsMismatch, issue.Kind);
        Assert.Contains("Yes 69", issue.Message);
        Assert.Contains("Yes 70", issue.Message);
        Assert.False(check.CanApprove);
    }

    [Fact]
    public void A_sheet_with_no_readable_printed_totals_cannot_be_checked_and_blocks()
    {
        var check = Check(SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), printed: null));

        Assert.Equal([SheetIssueKind.TotalsMissing], Blocking(check).Select(i => i.Kind));
    }

    [Fact]
    public void A_code_that_is_not_a_member_state_blocks_and_names_the_code()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 5) cells[0] = new("ZZ0", .93); });

        var issue = Assert.Single(Blocking(Check(read)), i => i.Kind == SheetIssueKind.UnknownCode);

        Assert.Equal("ZZ0", issue.Code);
    }

    [Fact]
    public void A_state_that_was_not_a_member_on_the_vote_date_blocks()
    {
        var members = SheetFixture.Members(from: new DateOnly(2012, 1, 1));

        var check = Check(Clean(), members);

        Assert.Equal(148, Blocking(check).Count(i => i.Kind == SheetIssueKind.NotMemberOnDate));
        Assert.Contains("2011-10-04", Blocking(check)[0].Message);
    }

    [Fact]
    public void A_code_that_appears_twice_blocks_once()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 9) cells[0] = new(SheetFixture.Code(3), .99); });

        var duplicates = Blocking(Check(read)).Where(i => i.Kind == SheetIssueKind.DuplicateCode).ToArray();

        Assert.Equal(SheetFixture.Code(3), Assert.Single(duplicates).Code);
    }

    [Fact]
    public void A_row_with_two_marks_blocks_and_stays_out_of_the_counts()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 0) cells[3] = new("X", .9); });

        var check = Check(read);

        Assert.Contains(Blocking(check), i => i.Kind == SheetIssueKind.AmbiguousMark && i.Code == SheetFixture.Code(0));
        Assert.Equal(69, check.Counted.Yes);
    }

    [Fact]
    public void A_header_that_cannot_be_read_as_a_vote_blocks()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011, session: "sixty-six", date: "sometime in autumn");

        var kinds = Blocking(Check(read)).Select(i => i.Kind).ToArray();

        Assert.Equal([SheetIssueKind.Header, SheetIssueKind.Header], kinds);
    }

    [Fact]
    public void A_low_confidence_cell_only_warns_with_the_row_s_code()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 20) cells[2] = new("X", .41); });

        var check = Check(read);

        var warning = Assert.Single(check.Issues);
        Assert.Equal((SheetIssueSeverity.Warning, SheetIssueKind.LowConfidence, SheetFixture.Code(20)), (warning.Severity, warning.Kind, warning.Code));
        Assert.True(check.CanApprove);
    }

    [Fact]
    public void A_header_vote_id_that_differs_from_the_file_name_only_warns()
    {
        var check = Check(Clean(), fileName: "MA_2011_043.pdf");

        Assert.Equal([SheetIssueKind.VoteIdMismatch], check.Issues.Select(i => i.Kind));
        Assert.True(check.CanApprove);
    }

    [Fact]
    public void A_file_name_that_adds_a_suffix_to_the_vote_id_matches()
    {
        Assert.Empty(Check(Clean(), fileName: "MA_2011_042_corrigendum.pdf").Issues);
    }

    [Fact]
    public void A_member_on_the_date_with_no_row_on_the_sheet_only_warns()
    {
        var check = Check(Clean(), SheetFixture.Members(149));

        var warning = Assert.Single(check.Issues);
        Assert.Equal((SheetIssueKind.MissingMember, SheetFixture.Code(148)), (warning.Kind, warning.Code));
        Assert.True(check.CanApprove);
    }

    [Fact]
    public void Checking_an_edited_sheet_again_clears_the_issue_the_edit_fixed()
    {
        var read = SheetFixture.Build(SheetFixture.Choices(SheetFixture.Printed2011), SheetFixture.Printed2011,
            tweak: (i, cells) => { if (i == 0) cells[3] = new("X", .9); });
        var mapped = SheetMapper.Map(read);
        var fixedRows = mapped.Rows.Select(r => r.Ambiguous ? r with { Choice = Ballot.Y, Ambiguous = false } : r).ToList();

        var check = SheetValidator.Check(mapped with { Rows = fixedRows }, FileName, SheetFixture.Members());

        Assert.Empty(check.Issues);
    }
}
