using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Integration.Tests.Snapshots.SnapshotCalculationTests;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>Every snapshot holds its name: no two snapshots share one, ignoring case and surrounding spaces.</summary>
public sealed class SnapshotNameTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> PostAsync(ApiFactory api, string name, long voteId) =>
        api.ClientAs("omar").PostAsJsonAsync("/api/snapshots", new { name, selection = new { voteIds = new[] { voteId } } }, Ct);

    [Fact]
    public async Task A_name_already_taken_is_refused_on_the_name_field_whatever_its_case_or_spacing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        await CreateAsync(api, new { voteIds = new[] { vote.Id } }, name: "Q1 2024 index");

        foreach (var taken in new[] { "Q1 2024 index", "  q1 2024 INDEX " })
        {
            var problem = await (await PostAsync(api, taken, vote.Id)).AssertProblemAsync(400, "validation");
            var message = Assert.Single(problem.GetProperty("errors").GetProperty("name").EnumerateArray()).GetString();
            Assert.Contains("Q1 2024 index", message);
        }

        Assert.Equal(202, (int)(await PostAsync(api, "Q1 2024 index (2)", vote.Id)).StatusCode);
    }

    [Fact]
    public async Task A_failed_snapshot_keeps_its_name()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        await CreateAsync(api, new { from = "2024-01-01", to = "2024-03-31" }, name: "Doomed");
        await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 1)); // no position: the job fails
        await RunJobsAsync(api.ConnectionString);

        await (await PostAsync(api, "Doomed", vote.Id)).AssertProblemAsync(400, "validation");
    }

    [Fact]
    public async Task Two_simultaneous_creates_with_one_name_give_one_snapshot_in_each_of_10_rounds()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);

        for (var round = 0; round < 10; round++)
        {
            var name = $"Race {round}";
            var responses = await RaceBarrier.RunTogetherAsync(2, _ => PostAsync(api, name, vote.Id));

            var statuses = responses.Select(r => (int)r.StatusCode).Order().ToArray();
            Assert.True(statuses is [202, 400], $"Round {round}: {string.Join(", ", statuses)}");
            await responses.Single(r => (int)r.StatusCode == 400).AssertProblemAsync(400, "validation");
        }
        var list = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/snapshots?pageSize=50", Ct);
        Assert.Equal(10, list.GetProperty("total").GetInt32());
    }
}
