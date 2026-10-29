using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Integration.Tests.Snapshots.SnapshotCalculationTests;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>SNP-05: one published snapshot per reporting quarter, replaced openly with a reason.</summary>
public sealed class PublicationTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(ApiFactory Api, Meridian.Fixtures.Fixtures Fx, Vote Vote)> ArrangeAsync(PostgresFixture postgres, AzuriteFixture azurite)
    {
        var api = await ApiFactory.CreateAsync(postgres, azurite);
        var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        return (api, fx, vote);
    }

    private static Task<HttpResponseMessage> PublishAsync(ApiFactory api, long snapshotId, object request) =>
        api.ClientAs("lena").PostAsJsonAsync($"/api/snapshots/{snapshotId}/publications", request, Ct);

    private static async Task<long> PublishedAsync(HttpResponseMessage response)
    {
        Assert.Equal(201, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task The_admin_publishes_a_calculated_snapshot_for_a_quarter_they_choose()
    {
        var (api, fx, vote) = await ArrangeAsync(postgres, azurite);
        await using var ownedApi = api;
        await using var ownedFx = fx;
        var snapshot = await fx.SnapshotAsync("Q1 2024 index", [vote]);

        var response = await PublishAsync(api, snapshot.Id, new { quarter = "2024-Q2" });

        Assert.Equal(201, (int)response.StatusCode);
        var publication = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((snapshot.Id, "Q1 2024 index", "2024-Q2", "lena"), (publication.GetProperty("snapshotId").GetInt64(),
            publication.GetProperty("snapshotName").GetString(), publication.GetProperty("quarter").GetString(),
            publication.GetProperty("publishedBy").GetString()));
        Assert.Equal(JsonValueKind.Null, publication.GetProperty("reason").ValueKind);
        var onSnapshot = (await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{snapshot.Id}", Ct))
            .GetProperty("publications");
        Assert.Equal("2024-Q2", Assert.Single(onSnapshot.EnumerateArray()).GetProperty("quarter").GetString());
    }

    [Fact]
    public async Task A_published_quarter_answers_409_and_a_replacement_with_a_reason_keeps_the_chain_visible()
    {
        var (api, fx, vote) = await ArrangeAsync(postgres, azurite);
        await using var ownedApi = api;
        await using var ownedFx = fx;
        var first = await fx.SnapshotAsync("First", [vote]);
        var second = await fx.SnapshotAsync("Corrected", [vote]);
        var original = await PublishedAsync(await PublishAsync(api, first.Id, new { quarter = "2024-Q1" }));

        var taken = await (await PublishAsync(api, second.Id, new { quarter = "2024-Q1" })).AssertProblemAsync(409, "quarter-published");
        Assert.Equal(original, taken.GetProperty("publication").GetProperty("id").GetInt64());

        var noReason = await PublishAsync(api, second.Id, new { quarter = "2024-Q1", supersedesPublicationId = original });
        Assert.True((await noReason.AssertProblemAsync(400, "validation")).GetProperty("errors").TryGetProperty("reason", out _));

        var replacement = await PublishedAsync(await PublishAsync(api, second.Id,
            new { quarter = "2024-Q1", supersedesPublicationId = original, reason = "Rev2 corrections accepted." }));

        var stale = await (await PublishAsync(api, first.Id,
            new { quarter = "2024-Q1", supersedesPublicationId = original, reason = "Back to the first." }))
            .AssertProblemAsync(409, "quarter-published");
        Assert.Equal(replacement, stale.GetProperty("publication").GetProperty("id").GetInt64());

        var quarters = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>("/api/publications", Ct);
        var quarter = Assert.Single(quarters.EnumerateArray());
        Assert.Equal(("2024-Q1", replacement), (quarter.GetProperty("quarter").GetString(), quarter.GetProperty("current").GetProperty("id").GetInt64()));
        var chain = quarter.GetProperty("chain").EnumerateArray().ToList();
        Assert.Equal([original, replacement], chain.Select(p => p.GetProperty("id").GetInt64()));
        Assert.Equal(replacement, chain[0].GetProperty("supersededByPublicationId").GetInt64());
        Assert.Equal(("Rev2 corrections accepted.", original),
            (chain[1].GetProperty("reason").GetString(), chain[1].GetProperty("supersedesPublicationId").GetInt64()));
    }

    [Fact]
    public async Task Quarters_are_listed_newest_first()
    {
        var (api, fx, vote) = await ArrangeAsync(postgres, azurite);
        await using var ownedApi = api;
        await using var ownedFx = fx;
        var snapshot = await fx.SnapshotAsync("Index", [vote]);
        foreach (var quarter in new[] { "2024-Q1", "2025-Q1", "2024-Q4" })
            await PublishedAsync(await PublishAsync(api, snapshot.Id, new { quarter }));

        var quarters = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/publications", Ct);

        Assert.Equal(["2025-Q1", "2024-Q4", "2024-Q1"], quarters.EnumerateArray().Select(q => q.GetProperty("quarter").GetString()));
    }

    [Fact]
    public async Task A_snapshot_that_is_not_calculated_or_does_not_exist_is_refused()
    {
        var (api, fx, vote) = await ArrangeAsync(postgres, azurite);
        await using var ownedApi = api;
        await using var ownedFx = fx;
        var queued = await CreateAsync(api, new { voteIds = new[] { vote.Id } });

        await (await PublishAsync(api, queued, new { quarter = "2024-Q1" })).AssertProblemAsync(400, "bad-request");
        await (await PublishAsync(api, 999999, new { quarter = "2024-Q1" })).AssertProblemAsync(404, "not-found");
        await (await PublishAsync(api, queued, new { quarter = "2024-Q5" })).AssertProblemAsync(400, "validation");
    }

    [Fact]
    public async Task Two_simultaneous_publications_for_one_quarter_give_one_success_and_one_409_in_each_of_20_rounds()
    {
        var (api, fx, vote) = await ArrangeAsync(postgres, azurite);
        await using var ownedApi = api;
        await using var ownedFx = fx;
        var snapshots = new[] { await fx.SnapshotAsync("A", [vote]), await fx.SnapshotAsync("B", [vote]) };

        for (var round = 0; round < 20; round++)
        {
            var quarter = $"{2000 + round}-Q3";
            var responses = await RaceBarrier.RunTogetherAsync(2, i => PublishAsync(api, snapshots[i].Id, new { quarter }));

            var statuses = responses.Select(r => (int)r.StatusCode).Order().ToArray();
            Assert.True(statuses is [201, 409], $"Round {round}: {string.Join(", ", statuses)}");
            var loser = responses.Single(r => (int)r.StatusCode == 409);
            var winner = await PublishedAsync(responses.Single(r => (int)r.StatusCode == 201));
            var problem = await loser.AssertProblemAsync(409, "quarter-published");
            Assert.Equal(winner, problem.GetProperty("publication").GetProperty("id").GetInt64());
        }
    }
}
