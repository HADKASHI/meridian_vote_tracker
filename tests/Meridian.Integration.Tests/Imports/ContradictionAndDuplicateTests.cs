using Meridian.Data;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Map ticket 08 rules 3 and 4 through the API.</summary>
public sealed class ContradictionAndDuplicateTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Contradictions_store_nothing_and_a_vote_whose_details_disagree_is_held_back_whole()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "contradictions.csv");

        Assert.Equal(new OutcomeCountsDto(1, 0, 0, 0, 5, 0), import.Counts);
        var perVote = await ImportApi.GetAsync<List<ImportVoteCountsDto>>(api.ClientAs("dana"), $"/api/imports/{import.Id}/votes");
        Assert.Equal([("MA/2024/012", 1, 2), ("MA/2024/025", 0, 3)],
            perVote.Select(v => (v.Vote.AssemblyVoteId, v.Counts.New, v.Counts.Contradiction)));

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(["MA/2024/012"], await db.CurrentVotes.Select(v => v.AssemblyVoteId).ToListAsync(Ct));
        Assert.Equal([("BRA", Ballot.Y)], (await db.CurrentCountryVotes.ToListAsync(Ct)).Select(c => (c.StateCode, c.Ballot)));
        Assert.True(await db.Votes.AnyAsync(v => v.AssemblyVoteId == "MA/2024/025", Ct));
    }

    [Fact]
    public async Task An_exact_repeat_is_a_duplicate_and_the_first_copy_is_approved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "duplicates.csv");

        Assert.Equal(new OutcomeCountsDto(2, 0, 1, 0, 0, 0), import.Counts);
        var duplicate = Assert.Single((await ImportApi.GetAsync<PageDto<ImportRowDto>>(api.ClientAs("dana"),
            $"/api/imports/{import.Id}/rows?outcome=duplicate")).Items);
        Assert.Equal((4, "Identical to line 2."), (duplicate.LineNo, duplicate.ReasonMessage));
    }
}
