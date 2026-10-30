using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Imports;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Meridian.Votes;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>Before approving, the analyst sees what approval would do, from the importer's own classifier, without writing anything.</summary>
public sealed class ExtractionPreviewTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Fact]
    public async Task A_first_sheet_for_a_vote_would_be_all_new_and_the_preview_writes_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("omar");

        var preview = await ExtractionApi.PreviewOkAsync(client, id);

        Assert.Equal((new OutcomeCountsDto(11, 0, 0, 0, 0, 0), false), (preview.Counts, preview.VoteExists));
        Assert.Empty(preview.ConflictStateCodes);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports")).Total);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<VoteSummaryDto>>(client, "/api/votes")).Total);
        Assert.Equal(ExtractionStatus.NeedsReview, (await ExtractionApi.GetAsync(client, id)).Status);
    }

    [Fact]
    public async Task A_corrigendum_would_leave_ten_unchanged_and_open_one_conflict_for_the_changed_state()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("omar");
        await ExtractionApi.ApproveOkAsync(client, await ExtractionApi.ReadyForReviewAsync(api));
        var corrigendum = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Corrigendum(), "MA_2011_042_corrigendum.pdf");

        var preview = await ExtractionApi.PreviewOkAsync(client, corrigendum);

        Assert.Equal((new OutcomeCountsDto(0, 10, 0, 0, 0, 1), true), (preview.Counts, preview.VoteExists));
        Assert.Equal(["SSD"], preview.ConflictStateCodes);
        Assert.Equal(0, (await ImportApi.GetAsync<PageDto<Meridian.Conflicts.ConflictListItemDto>>(client, "/api/conflicts")).Total);
        Assert.Equal(1, (await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports")).Total);
    }

    [Fact]
    public async Task What_the_preview_says_is_what_approval_then_does()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("omar");
        await ExtractionApi.ApproveOkAsync(client, await ExtractionApi.ReadyForReviewAsync(api));
        var corrigendum = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Corrigendum(), "MA_2011_042_corrigendum.pdf");
        var preview = await ExtractionApi.PreviewOkAsync(client, corrigendum);

        var approved = await ExtractionApi.ApproveOkAsync(client, corrigendum);

        Assert.Equal(preview.Counts, (await ImportApi.GetAsync<ImportDto>(client, $"/api/imports/{approved.ImportId}")).Counts);
    }

    [Fact]
    public async Task A_sheet_that_still_has_a_blocking_issue_has_no_preview()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api, SheetFixtures.Sheet(new SheetTotals(5, 3, 1, 1)));

        await (await ExtractionApi.PreviewAsync(api.ClientAs("omar"), id)).AssertProblemAsync(409, "has-blocking-issues");
    }

    [Fact]
    public async Task A_decided_one_or_an_unknown_one_has_no_preview()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        await ExtractionApi.RejectAsync(api.ClientAs("omar"), id);

        await (await ExtractionApi.PreviewAsync(api.ClientAs("dana"), id)).AssertProblemAsync(409, "already-decided");
        await (await ExtractionApi.PreviewAsync(api.ClientAs("dana"), 999)).AssertProblemAsync(404, "not-found");
    }
}
