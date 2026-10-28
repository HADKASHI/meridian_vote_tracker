using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;
using static Meridian.Integration.Tests.Snapshots.SnapshotCalculationTests;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>SNP-03, SNP-04: a state's breakdown and the snapshots list read what the snapshot stored.</summary>
public sealed class SnapshotBreakdownTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_breakdown_lists_every_vote_with_the_values_the_snapshot_used_even_after_they_change()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var first = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10), title: "Ocean Treaty");
        var used = (await fx.CountryVotesAsync(first, ("BRA", Y)))[0];
        await fx.PositionAsync(first, Stance.Yes, 3);
        var second = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 20), title: "Space Debris");
        await fx.CountryVotesAsync(second, ("BRA", A));
        await fx.PositionAsync(second, Stance.No, 1);
        var snapshotId = await CreateAsync(api, new { from = "2024-01-01", to = "2024-03-31" });
        await RunJobsAsync(api.ConnectionString);

        await fx.DecideAsync(await fx.BallotConflictAsync(first, "BRA", N), Decision.Accept);
        await fx.DecideAsync(await fx.DetailsConflictAsync(first, title: "Ocean Treaty (revised)"), Decision.Accept);
        await fx.PositionAsync(first, Stance.No, 5);

        var breakdown = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{snapshotId}/states/BRA", Ct);

        var result = breakdown.GetProperty("result");
        Assert.Equal(("BRA", "Brazil", 3, 3, 1, 1), (result.GetProperty("stateCode").GetString(), result.GetProperty("stateName").GetString(),
            result.GetProperty("matchedWeight").GetInt32(), result.GetProperty("countedWeight").GetInt32(),
            result.GetProperty("countableVotes").GetInt32(), result.GetProperty("rank").GetInt32()));
        var lines = breakdown.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(("MA/2024/001", "2024-01-10", "Ocean Treaty", "yes", 3, "y", "match", used.Id), (
            lines[0].GetProperty("vote").GetProperty("assemblyVoteId").GetString(), lines[0].GetProperty("voteDate").GetString(),
            lines[0].GetProperty("title").GetString(), lines[0].GetProperty("stance").GetString(), lines[0].GetProperty("weight").GetInt32(),
            lines[0].GetProperty("ballot").GetString(), lines[0].GetProperty("category").GetString(),
            lines[0].GetProperty("countryVoteRevisionId").GetInt64()));
        Assert.Equal(("MA/2024/002", "a", "abstain"), (lines[1].GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            lines[1].GetProperty("ballot").GetString(), lines[1].GetProperty("category").GetString()));
    }

    [Fact]
    public async Task A_member_with_no_country_vote_has_a_no_data_line_without_a_ballot()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var snapshotId = await CreateAsync(api, new { voteIds = new[] { vote.Id } });
        await RunJobsAsync(api.ConnectionString);

        var breakdown = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{snapshotId}/states/TUR", Ct);

        var line = Assert.Single(breakdown.GetProperty("lines").EnumerateArray());
        Assert.Equal("no_data", line.GetProperty("category").GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("ballot").ValueKind);
        Assert.Equal(JsonValueKind.Null, line.GetProperty("countryVoteRevisionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, breakdown.GetProperty("result").GetProperty("score").ValueKind);
    }

    [Fact]
    public async Task An_unknown_snapshot_or_a_state_outside_it_is_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var snapshot = await fx.SnapshotAsync("Q1", [vote]);
        var client = api.ClientAs("dana");

        await (await client.GetAsync($"/api/snapshots/{snapshot.Id}/states/XXX", Ct)).AssertProblemAsync(404, "not-found");
        await (await client.GetAsync("/api/snapshots/999999/states/FRA", Ct)).AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task The_list_is_newest_first_with_vote_counts_once_calculated_and_publications()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var published = await fx.SnapshotAsync("Published", [vote], createdBy: "omar");
        await fx.PublishAsync(published, "2024-Q1");
        var queued = await CreateAsync(api, new { voteIds = new[] { vote.Id } }, name: "Still queued");

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/snapshots?page=1&pageSize=1", Ct);

        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var newest = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal((queued, "Still queued", "dana", JsonValueKind.Null, "queued"), (newest.GetProperty("id").GetInt64(),
            newest.GetProperty("name").GetString(), newest.GetProperty("createdBy").GetString(),
            newest.GetProperty("voteCount").ValueKind, newest.GetProperty("job").GetProperty("state").GetString()));

        var second = (await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/snapshots?page=2&pageSize=1", Ct))
            .GetProperty("items")[0];
        Assert.Equal((published.Id, 1), (second.GetProperty("id").GetInt64(), second.GetProperty("voteCount").GetInt32()));
        Assert.Equal("2024-Q1", Assert.Single(second.GetProperty("publications").EnumerateArray()).GetProperty("quarter").GetString());
    }
}
