using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Conflicts;

/// <summary>The Review inbox: GET /api/conflicts (stories 50, 63, 64).</summary>
public sealed class ReviewInboxTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pending_conflicts_are_listed_by_vote_then_details_before_ballots_by_state()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var first = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 15));
        var second = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 20));
        await fx.CountryVotesAsync(first, ("FRA", Y), ("BRA", N));
        await fx.CountryVotesAsync(second, ("ARG", Y));
        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "rev2.csv");
        await fx.BallotConflictAsync(second, "ARG", N, openedBy: rev2);
        await fx.BallotConflictAsync(first, "FRA", A, openedBy: rev2);
        await fx.DetailsConflictAsync(first, voteDate: new DateOnly(2024, 1, 16), openedBy: rev2);
        await fx.BallotConflictAsync(first, "BRA", Y, openedBy: rev2);

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/conflicts", Ct);

        Assert.Equal(4, page.GetProperty("total").GetInt32());
        Assert.Equal(
            ["MA/2024/001 details ", "MA/2024/001 ballot BRA", "MA/2024/001 ballot FRA", "MA/2024/002 ballot ARG"],
            page.GetProperty("items").EnumerateArray().Select(i =>
                $"{i.GetProperty("vote").GetProperty("assemblyVoteId")} {i.GetProperty("subject")} {i.GetProperty("stateCode")}"));
        var fra = page.GetProperty("items")[2];
        Assert.Equal("conflict", fra.GetProperty("kind").GetString());
        Assert.Equal("y", fra.GetProperty("current").GetProperty("ballot").GetString());
        Assert.Equal("a", fra.GetProperty("incoming").GetProperty("ballot").GetString());
        Assert.Equal(rev2.Id, fra.GetProperty("importId").GetInt64());
        Assert.Equal(JsonValueKind.Null, fra.GetProperty("decision").ValueKind);
        var details = page.GetProperty("items")[0];
        Assert.Equal("2024-01-15", details.GetProperty("current").GetProperty("voteDate").GetString());
        Assert.Equal("2024-01-16", details.GetProperty("incoming").GetProperty("voteDate").GetString());
    }

    [Fact]
    public async Task The_current_value_is_the_approved_value_now_not_the_one_the_conflict_was_compared_against()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var toAbstain = await fx.BallotConflictAsync(vote, "BRA", A);
        await fx.BallotConflictAsync(vote, "BRA", N);
        await fx.DecideAsync(toAbstain, Decision.Accept);

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/conflicts", Ct);

        var pending = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(("a", "n"),
            (pending.GetProperty("current").GetProperty("ballot").GetString(), pending.GetProperty("incoming").GetProperty("ballot").GetString()));
    }

    [Fact]
    public async Task Decided_conflicts_stay_listed_with_their_decision_under_the_decided_and_all_filters()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y), ("FRA", N));
        var rejected = await fx.BallotConflictAsync(vote, "BRA", A);
        await fx.BallotConflictAsync(vote, "FRA", Y);
        await fx.DecideAsync(rejected, Decision.Reject);
        var client = api.ClientAs("omar");

        var decided = await client.GetFromJsonAsync<JsonElement>("/api/conflicts?state=decided", Ct);
        var all = await client.GetFromJsonAsync<JsonElement>("/api/conflicts?state=all", Ct);

        var item = Assert.Single(decided.GetProperty("items").EnumerateArray());
        Assert.Equal((rejected.Id, "reject"), (item.GetProperty("id").GetInt64(), item.GetProperty("decision").GetString()));
        Assert.Equal(["BRA", "FRA"], all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("stateCode").GetString()));
    }

    [Fact]
    public async Task The_inbox_narrows_to_some_votes_or_one_import_and_pages()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var votes = new List<Meridian.Data.Entities.Vote>();
        for (var i = 1; i <= 3; i++)
        {
            var vote = await fx.VoteAsync($"MA/2024/00{i}", new DateOnly(2024, 1, i));
            await fx.CountryVotesAsync(vote, ("FRA", Y));
            votes.Add(vote);
        }
        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "rev2.csv");
        var rev3 = await fx.ImportAsync(uploadedBy: "dana", fileName: "rev3.csv");
        await fx.BallotConflictAsync(votes[0], "FRA", N, openedBy: rev2);
        await fx.BallotConflictAsync(votes[1], "FRA", N, openedBy: rev3);
        await fx.BallotConflictAsync(votes[2], "FRA", N, openedBy: rev2);
        var client = api.ClientAs("dana");

        var byVotes = await client.GetFromJsonAsync<JsonElement>(
            $"/api/conflicts?voteIds={votes[0].Id}&voteIds={votes[1].Id}", Ct);
        var byImport = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts?importId={rev2.Id}", Ct);
        var secondPage = await client.GetFromJsonAsync<JsonElement>("/api/conflicts?page=2&pageSize=2", Ct);

        Assert.Equal(["MA/2024/001", "MA/2024/002"], AssemblyIds(byVotes));
        Assert.Equal(["MA/2024/001", "MA/2024/003"], AssemblyIds(byImport));
        Assert.Equal(["MA/2024/003"], AssemblyIds(secondPage));
        Assert.Equal((3, 2, 2), (secondPage.GetProperty("total").GetInt32(),
            secondPage.GetProperty("page").GetInt32(), secondPage.GetProperty("pageSize").GetInt32()));
    }

    [Fact]
    public async Task An_unknown_filter_is_a_validation_problem_on_state()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/conflicts?state=open", Ct);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("state", out _));
    }

    private static IEnumerable<string?> AssemblyIds(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("vote").GetProperty("assemblyVoteId").GetString());
}
