using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Imports;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>PDF-04: the analyst fixes what the reader got wrong, or rejects the sheet.</summary>
public sealed class ExtractionReviewTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>ARG (row 0) has both YES and NO marked, which the sheet's own totals can't settle.</summary>
    private static SheetRead TwoMarksOnArgentina() =>
        SheetFixtures.Sheet(tweak: (i, cells) => { if (i == 0) cells[3] = new SheetCell("X", .6); });

    [Fact]
    public async Task Choosing_the_ballot_of_a_row_with_two_marks_clears_what_blocked_the_sheet()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, TwoMarksOnArgentina());
        var client = api.ClientAs("omar");
        var before = await ExtractionApi.GetAsync(client, id);
        Assert.Contains(before.Issues, i => i.Kind == SheetIssueKind.AmbiguousMark && i.Code == "ARG");
        Assert.False(before.CanApprove);

        var after = await ExtractionApi.SaveEditsOkAsync(client, id, new SaveEditsRequest([new RowEdit(1, null, Ballot.Y)], null, null));

        Assert.Empty(after.Issues);
        Assert.True(after.CanApprove);
        Assert.Equal((Ballot.Y, false), (after.Rows[0].Choice!.Value, after.Rows[0].Ambiguous));
        Assert.Equal(SheetFixtures.Printed, after.Counted);
    }

    [Fact]
    public async Task The_extracted_value_the_editor_and_the_time_are_kept_beside_each_edit()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, TwoMarksOnArgentina());
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var after = await ExtractionApi.SaveEditsOkAsync(api.ClientAs("omar"), id, new SaveEditsRequest([new RowEdit(1, null, Ballot.Y)], null, null));

        var edit = Assert.Single(after.Edits);
        Assert.Equal(("omar", 1, "ARG", "choice", (string?)null, "Y"), (edit.By, edit.Position, edit.Code, edit.Field, edit.From, edit.To));
        Assert.True(edit.At > before);
    }

    [Fact]
    public async Task Correcting_a_misread_code_clears_the_unknown_code_and_the_missing_member()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Sheet(tweak: (i, cells) => { if (i == 6) cells[0] = new SheetCell("MN3", .55); }));
        var before = await ExtractionApi.GetAsync(api.ClientAs("dana"), id);
        Assert.Contains(before.Issues, i => i.Kind == SheetIssueKind.UnknownCode && i.Code == "MN3");

        var after = await ExtractionApi.SaveEditsOkAsync(api.ClientAs("dana"), id, new SaveEditsRequest([new RowEdit(7, " mne ", null)], null, null));

        Assert.Equal("MNE", after.Rows[6].Code);
        Assert.Empty(after.Issues);
    }

    /// <summary>A row for a code that is no state (ARG's slot, misread as ZZZ) which the sheet's own totals leave out.</summary>
    private static SheetRead SpuriousRowOnTop() =>
        SheetFixtures.Sheet(new SheetTotals(4, 3, 2, 1), (i, cells) => { if (i == 0) cells[0] = new SheetCell("ZZZ", .9); });

    [Fact]
    public async Task Removing_a_row_with_an_issue_treats_it_as_never_read_so_it_no_longer_blocks_and_the_approval_leaves_it_out()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SpuriousRowOnTop());
        var client = api.ClientAs("omar");
        var before = await ExtractionApi.GetAsync(client, id);
        Assert.Contains(before.Issues, i => i.Kind == SheetIssueKind.UnknownCode && i.Code == "ZZZ");
        Assert.False(before.CanApprove);

        var after = await ExtractionApi.SaveEditsOkAsync(client, id, new SaveEditsRequest([new RowEdit(1, null, null, true)], null, null));

        Assert.True(after.CanApprove);
        Assert.Equal(SheetFixtures.Members.Skip(1), after.Rows.Select(r => r.Code));
        Assert.Equal(SheetKinds(after), [SheetIssueKind.MissingMember]);
        var edit = Assert.Single(after.Edits);
        Assert.Equal(("omar", 1, "ZZZ", "removed", "Y", (string?)null), (edit.By, edit.Position, edit.Code, edit.Field, edit.From, edit.To));

        var approved = await ExtractionApi.ApproveOkAsync(client, id);
        var rows = await ImportApi.GetAsync<PageDto<ImportRowDto>>(client, $"/api/imports/{approved.ImportId}/rows");
        Assert.Equal(SheetFixtures.Members.Skip(1), rows.Items.Select(r => r.StateCode));
    }

    private static IEnumerable<SheetIssueKind> SheetKinds(ExtractionDto e) => e.Issues.Select(i => i.Kind);

    [Fact]
    public async Task A_row_without_an_issue_cannot_be_removed_and_the_sheet_is_left_as_it_was()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("omar");

        var response = await ExtractionApi.SaveEditsAsync(client, id, new SaveEditsRequest([new RowEdit(1, null, null, true)], null, null));

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("rows[0].remove", out _));
        var sheet = await ExtractionApi.GetAsync(client, id);
        Assert.Equal((11, 0), (sheet.Rows.Count, sheet.Edits.Count));
    }

    [Fact]
    public async Task Correcting_an_unreadable_date_in_the_header_lets_the_sheet_be_read_as_a_vote()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Sheet(date: "4 Octobre 2O11"));
        var before = await ExtractionApi.GetAsync(api.ClientAs("dana"), id);
        Assert.Contains(before.Issues, i => i.Kind == SheetIssueKind.Header);

        var after = await ExtractionApi.SaveEditsOkAsync(api.ClientAs("dana"), id,
            new SaveEditsRequest(null, new HeaderEdit(null, null, "4 October 2011", null), null));

        Assert.Empty(after.Issues);
        Assert.Equal("4 October 2011", after.Header!.Date);
        Assert.Equal(("date", "4 Octobre 2O11", "4 October 2011"), (after.Edits[0].Field, after.Edits[0].From, after.Edits[0].To));
    }

    [Fact]
    public async Task Entering_the_printed_totals_by_hand_when_the_footer_was_unreadable_allows_the_check()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Sheet(omitTotals: true));
        Assert.Contains((await ExtractionApi.GetAsync(api.ClientAs("dana"), id)).Issues, i => i.Kind == SheetIssueKind.TotalsMissing);

        var after = await ExtractionApi.SaveEditsOkAsync(api.ClientAs("dana"), id, new SaveEditsRequest(null, null, SheetFixtures.Printed));

        Assert.Empty(after.Issues);
        Assert.Equal(SheetFixtures.Printed, after.Printed);
    }

    [Fact]
    public async Task An_edit_that_does_not_fix_the_sheet_leaves_it_blocked_and_says_why()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);

        var after = await ExtractionApi.SaveEditsOkAsync(api.ClientAs("dana"), id, new SaveEditsRequest([new RowEdit(1, null, Ballot.N)], null, null));

        Assert.Equal([SheetIssueKind.TotalsMismatch], after.Issues.Select(i => i.Kind));
        Assert.False(after.CanApprove);
    }

    [Fact]
    public async Task Edits_to_a_row_that_does_not_exist_or_nothing_at_all_are_validation_problems()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("dana");

        var unknownRow = await ExtractionApi.SaveEditsAsync(client, id, new SaveEditsRequest([new RowEdit(99, null, Ballot.Y)], null, null));
        var problem = await unknownRow.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("rows[0].position", out _));

        var nothing = await ExtractionApi.SaveEditsAsync(client, id, new SaveEditsRequest(null, null, null));
        await nothing.AssertProblemAsync(400, "validation");
    }

    [Fact]
    public async Task Rejecting_ends_the_item_with_who_when_and_why_and_writes_nothing_to_approved_data()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("omar");

        var response = await ExtractionApi.RejectAsync(client, id, "Not the sheet for this vote.");

        Assert.Equal(200, (int)response.StatusCode);
        var extraction = await ExtractionApi.GetAsync(client, id);
        Assert.Equal(ExtractionStatus.Rejected, extraction.Status);
        Assert.Equal(("omar", "Not the sheet for this vote."), (extraction.Decision!.DecidedBy, extraction.Decision.Comment));
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports")).Total);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<JsonElement>>(client, "/api/votes")).Total);
    }

    [Fact]
    public async Task A_second_decision_is_refused_with_409_naming_the_winner_and_edits_after_it_are_refused_too()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        await ExtractionApi.RejectAsync(api.ClientAs("omar"), id, "First.");

        var second = await ExtractionApi.RejectAsync(api.ClientAs("dana"), id, "Second.");

        var problem = await second.AssertProblemAsync(409, "already-decided");
        Assert.Equal("omar", problem.GetProperty("decision").GetProperty("decidedBy").GetString());
        Assert.Equal("First.", problem.GetProperty("decision").GetProperty("comment").GetString());
        var edit = await ExtractionApi.SaveEditsAsync(api.ClientAs("dana"), id, new SaveEditsRequest([new RowEdit(1, null, Ballot.N)], null, null));
        await edit.AssertProblemAsync(409, "already-decided");
        Assert.Equal("Rejected", (await ExtractionApi.GetAsync(api.ClientAs("dana"), id)).Status.ToString());
    }

    [Fact]
    public async Task A_sheet_still_being_read_can_be_neither_edited_nor_rejected()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        var edit = await ExtractionApi.SaveEditsAsync(api.ClientAs("dana"), created.ExtractionId, new SaveEditsRequest(null, null, SheetFixtures.Printed));
        var reject = await ExtractionApi.RejectAsync(api.ClientAs("dana"), created.ExtractionId);

        await edit.AssertProblemAsync(409, "not-reviewable");
        await reject.AssertProblemAsync(409, "not-reviewable");
    }

    [Fact]
    public async Task Editing_or_rejecting_an_unknown_extraction_is_404()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        await (await ExtractionApi.RejectAsync(api.ClientAs("dana"), 999)).AssertProblemAsync(404, "not-found");
        await (await ExtractionApi.SaveEditsAsync(api.ClientAs("dana"), 999, new SaveEditsRequest(null, null, SheetFixtures.Printed))).AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task Two_analysts_rejecting_at_once_one_wins_and_the_other_gets_409_in_every_round()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        const int rounds = 20;
        var ids = new List<long>();
        for (var i = 0; i < rounds; i++)
            ids.Add((await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), $"round-{i}.pdf", [.. System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4 round {i}")])).ExtractionId);
        await ExtractionApi.RunJobsAsync(api, new FakeSheetReader(SheetFixtures.Sheet()));

        foreach (var id in ids)
        {
            var results = await RaceBarrier.RunTogetherAsync(2, async who =>
            {
                var response = await ExtractionApi.RejectAsync(api.ClientAs(who == 0 ? "dana" : "omar"), id, $"by {who}");
                return (User: who == 0 ? "dana" : "omar", Status: (int)response.StatusCode);
            });

            Assert.Equal([200, 409], results.Select(r => r.Status).Order());
            var winner = results.Single(r => r.Status == 200).User;
            Assert.Equal(winner, (await ExtractionApi.GetAsync(api.ClientAs("dana"), id)).Decision!.DecidedBy);
        }
    }
}
