using System.Net.Http.Json;
using Meridian.Conflicts;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Imports;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Meridian.Votes;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>PDF-05: an approved extraction follows the CSV's own rules, and nothing overwrites approved data without a person deciding.</summary>
public sealed class ExtractionApprovalTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<VoteDto> TheVoteAsync(HttpClient client)
    {
        var votes = await ImportApi.GetAsync<PageDto<VoteSummaryDto>>(client, "/api/votes");
        return await ImportApi.GetAsync<VoteDto>(client, $"/api/votes/{votes.Items.Single(v => v.AssemblyVoteId == "MA/2011/042").Id}");
    }

    [Fact]
    public async Task The_first_sheet_for_a_vote_is_approved_as_an_import_of_new_rows_with_the_sheet_as_its_file()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("omar");

        var approved = await ExtractionApi.ApproveOkAsync(client, id, "Checked against the original.");

        var extraction = await ExtractionApi.GetAsync(client, id);
        Assert.Equal((ExtractionStatus.Approved, "omar", "Checked against the original.", approved.ImportId),
            (extraction.Status, extraction.Decision!.DecidedBy, extraction.Decision.Comment, extraction.ImportId));
        var import = await ImportApi.GetAsync<ImportDto>(client, $"/api/imports/{approved.ImportId}");
        Assert.Equal(("MA_2011_042.pdf", "dana", JobState.Done), (import.FileName, import.UploadedBy, import.Job.State));
        Assert.Equal(new OutcomeCountsDto(11, 0, 0, 0, 0, 0), import.Counts);
        Assert.Empty(import.ConflictsOpened);

        var vote = await TheVoteAsync(client);
        Assert.Equal((66, new DateOnly(2011, 10, 4), "Protection of Cultural Heritage"), (vote.Details!.Session, vote.Details.VoteDate, vote.Details.Title));
        var ballots = vote.CountryVotes.Where(c => c.Ballot is not null).ToDictionary(c => c.StateCode, c => c.Ballot!.Value);
        Assert.Equal(SheetFixtures.Members.Zip(SheetFixtures.Choices).ToDictionary(p => p.First, p => p.Second), ballots);
    }

    [Fact]
    public async Task The_rows_of_the_import_are_numbered_by_sheet_order_and_say_which_state_they_are()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("omar");

        var approved = await ExtractionApi.ApproveOkAsync(client, id);

        var rows = await ImportApi.GetAsync<PageDto<ImportRowDto>>(client, $"/api/imports/{approved.ImportId}/rows");
        Assert.Equal(Enumerable.Range(1, 11), rows.Items.Select(r => r.LineNo));
        Assert.Equal(SheetFixtures.Members, rows.Items.Select(r => r.StateCode));
        Assert.All(rows.Items, r => Assert.Equal(Outcome.New, r.Outcome));
        Assert.Contains("ARG", rows.Items[0].RawText);
    }

    [Fact]
    public async Task The_same_votes_read_from_another_scan_are_unchanged()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await ExtractionApi.ApproveOkAsync(api.ClientAs("omar"), await ExtractionApi.ReadyForReviewAsync(api));
        var second = await ExtractionApi.ReadyForReviewAsync(api, name: "MA_2011_042_rescan.pdf");

        var approved = await ExtractionApi.ApproveOkAsync(api.ClientAs("omar"), second);

        var import = await ImportApi.GetAsync<ImportDto>(api.ClientAs("omar"), $"/api/imports/{approved.ImportId}");
        Assert.Equal(new OutcomeCountsDto(0, 11, 0, 0, 0, 0), import.Counts);
    }

    [Fact]
    public async Task A_corrigendum_gives_one_conflict_for_the_changed_state_and_leaves_approved_data_as_it_was()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("omar");
        await ExtractionApi.ApproveOkAsync(client, await ExtractionApi.ReadyForReviewAsync(api));
        var corrigendum = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Corrigendum(), "MA_2011_042_corrigendum.pdf");

        var approved = await ExtractionApi.ApproveOkAsync(client, corrigendum);

        var import = await ImportApi.GetAsync<ImportDto>(client, $"/api/imports/{approved.ImportId}");
        Assert.Equal(new OutcomeCountsDto(0, 10, 0, 0, 0, 1), import.Counts);
        var opened = Assert.Single(import.ConflictsOpened);
        Assert.Equal((ConflictSubject.Ballot, "SSD"), (opened.Subject, opened.StateCode));
        var pending = await ImportApi.GetAsync<PageDto<ConflictListItemDto>>(client, "/api/conflicts");
        var conflict = Assert.Single(pending.Items);
        Assert.Equal((Ballot.Y, Ballot.A), (conflict.Current!.Ballot, conflict.Incoming.Ballot));
        Assert.Equal(Ballot.Y, (await TheVoteAsync(client)).CountryVotes.Single(c => c.StateCode == "SSD").Ballot);
    }

    [Fact]
    public async Task A_sheet_that_still_has_a_blocking_issue_cannot_be_approved_and_writes_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Sheet(new SheetTotals(5, 3, 1, 1)));
        var client = api.ClientAs("omar");

        var response = await ExtractionApi.ApproveAsync(client, id);

        await response.AssertProblemAsync(409, "has-blocking-issues");
        Assert.Equal(ExtractionStatus.NeedsReview, (await ExtractionApi.GetAsync(client, id)).Status);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports")).Total);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<VoteSummaryDto>>(client, "/api/votes")).Total);
    }

    [Fact]
    public async Task What_the_analyst_edited_is_what_is_imported()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api,
            SheetFixtures.Sheet(tweak: (i, cells) => { if (i == 0) cells[3] = new SheetCell("X", .6); }));
        var client = api.ClientAs("omar");
        await ExtractionApi.SaveEditsOkAsync(client, id, new SaveEditsRequest([new RowEdit(1, null, Ballot.Y)], null, null));

        await ExtractionApi.ApproveOkAsync(client, id);

        Assert.Equal(Ballot.Y, (await TheVoteAsync(client)).CountryVotes.Single(c => c.StateCode == "ARG").Ballot);
    }

    [Fact]
    public async Task A_second_approval_or_an_approval_after_a_rejection_is_refused_with_409_naming_the_winner()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var approvedId = await ExtractionApi.ReadyForReviewAsync(api);
        var rejectedId = await ExtractionApi.ReadyForReviewAsync(api);
        await ExtractionApi.ApproveOkAsync(api.ClientAs("omar"), approvedId);
        await ExtractionApi.RejectAsync(api.ClientAs("omar"), rejectedId, "No.");

        var again = await ExtractionApi.ApproveAsync(api.ClientAs("dana"), approvedId);
        var afterRejection = await ExtractionApi.ApproveAsync(api.ClientAs("dana"), rejectedId);

        var problem = await again.AssertProblemAsync(409, "already-decided");
        Assert.Equal("omar", problem.GetProperty("decision").GetProperty("decidedBy").GetString());
        await afterRejection.AssertProblemAsync(409, "already-decided");
        Assert.Equal(1, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(api.ClientAs("dana"), "/api/imports")).Total);
    }

    [Fact]
    public async Task A_sheet_still_being_read_or_an_unknown_one_cannot_be_approved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        await (await ExtractionApi.ApproveAsync(api.ClientAs("dana"), created.ExtractionId)).AssertProblemAsync(409, "not-reviewable");
        await (await ExtractionApi.ApproveAsync(api.ClientAs("dana"), 999)).AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task The_import_of_a_sheet_shows_in_the_imports_list_with_its_finished_job()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("omar");
        var approved = await ExtractionApi.ApproveOkAsync(client, await ExtractionApi.ReadyForReviewAsync(api));

        var page = await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports");

        var item = Assert.Single(page.Items);
        Assert.Equal((approved.ImportId, "MA_2011_042.pdf", JobState.Done), (item.Id, item.FileName, item.Job.State));
        Assert.Equal(11, item.Counts.New);
    }

    [Fact]
    public async Task Two_analysts_approving_at_once_one_wins_and_exactly_one_import_is_made_in_every_round()
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
                (int)(await ExtractionApi.ApproveAsync(api.ClientAs(who == 0 ? "dana" : "omar"), id)).StatusCode);

            Assert.Equal([200, 409], results.Order());
        }
        Assert.Equal(rounds, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(api.ClientAs("dana"), "/api/imports")).Total);
    }
}
