using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Positions;

/// <summary>POS-02 to POS-04: the admin sets a stance and weight, conditional on the position they saw.</summary>
public sealed class SetPositionTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, long voteId, object body) =>
        client.PutAsJsonAsync($"/api/votes/{voteId}/position", body, Ct);

    [Fact]
    public async Task The_admin_sets_a_first_position_and_the_worklist_shows_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));

        var response = await SaveAsync(api.ClientAs("lena"), vote.Id, new { stance = "no", weight = 5, seenRevisionId = (long?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(("no", 5, "lena"),
            (saved.GetProperty("stance").GetString(), saved.GetProperty("weight").GetInt32(), saved.GetProperty("setBy").GetString()));
        var worklist = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/positions", Ct);
        var shown = worklist.GetProperty("items")[0].GetProperty("position");
        Assert.Equal(saved.GetProperty("revisionId").GetInt64(), shown.GetProperty("revisionId").GetInt64());
    }

    [Fact]
    public async Task Changing_a_position_adds_a_revision_and_keeps_the_earlier_one()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        var earlier = await fx.PositionAsync(vote, Stance.Yes, 2);

        var response = await SaveAsync(api.ClientAs("lena"), vote.Id, new { stance = "yes", weight = 4, seenRevisionId = earlier.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.NotEqual(earlier.Id, saved.GetProperty("revisionId").GetInt64());
        Assert.Equal(4, saved.GetProperty("weight").GetInt32());
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(2, await db.VoteTimeline.CountAsync(e => e.VoteId == vote.Id && e.Kind == "position_revision", Ct));
    }

    [Fact]
    public async Task An_analyst_may_not_set_a_position()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));

        var response = await SaveAsync(api.ClientAs("omar"), vote.Id, new { stance = "yes", weight = 3, seenRevisionId = (long?)null });

        await response.AssertProblemAsync(403, "forbidden");
        var worklist = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>("/api/positions", Ct);
        Assert.Equal(JsonValueKind.Null, worklist.GetProperty("items")[0].GetProperty("position").ValueKind);
    }

    [Fact]
    public async Task A_missing_stance_and_an_out_of_range_weight_are_refused_by_field()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));

        var response = await SaveAsync(api.ClientAs("lena"), vote.Id, new { weight = 6 });

        var problem = await response.AssertProblemAsync(400, "validation");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("stance", out _));
        Assert.True(errors.TryGetProperty("weight", out _));
    }

    [Fact]
    public async Task An_unknown_vote_is_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await SaveAsync(api.ClientAs("lena"), 999_999, new { stance = "yes", weight = 3 });

        await response.AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task A_stale_save_is_refused_with_the_current_position_and_changes_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        var seen = await fx.PositionAsync(vote, Stance.Yes, 2);
        var theirs = await fx.PositionAsync(vote, Stance.No, 4);

        var response = await SaveAsync(api.ClientAs("lena"), vote.Id, new { stance = "yes", weight = 5, seenRevisionId = seen.Id });

        var problem = await response.AssertProblemAsync(409, "stale-position");
        var current = problem.GetProperty("current");
        Assert.Equal((theirs.Id, "no", 4),
            (current.GetProperty("revisionId").GetInt64(), current.GetProperty("stance").GetString(), current.GetProperty("weight").GetInt32()));
        var worklist = await api.ClientAs("lena").GetFromJsonAsync<JsonElement>("/api/positions", Ct);
        Assert.Equal(theirs.Id, worklist.GetProperty("items")[0].GetProperty("position").GetProperty("revisionId").GetInt64());
    }

    [Fact]
    public async Task A_first_save_is_stale_once_someone_else_set_a_position()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.No, 1);

        var response = await SaveAsync(api.ClientAs("lena"), vote.Id, new { stance = "yes", weight = 3, seenRevisionId = (long?)null });

        await response.AssertProblemAsync(409, "stale-position");
    }

    [Fact]
    public async Task Two_saves_of_the_same_seen_position_give_exactly_one_success_and_one_409_over_20_rounds()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var clients = new[] { api.ClientAs("lena"), api.ClientAs("lena") };

        for (var round = 1; round <= 20; round++)
        {
            var vote = await fx.VoteAsync($"MA/2024/{round:000}", new DateOnly(2024, 1, 10));
            long? seen = round % 2 == 0 ? (await fx.PositionAsync(vote, Stance.Yes, 1)).Id : null;

            var responses = await RaceBarrier.RunTogetherAsync(2, i =>
                SaveAsync(clients[i], vote.Id, new { stance = i == 0 ? "yes" : "no", weight = 3 + i, seenRevisionId = seen }));

            var statuses = responses.Select(r => (int)r.StatusCode).Order().ToArray();
            Assert.True(statuses is [200, 409], $"Round {round}: {string.Join(", ", statuses)}");
            await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).AssertProblemAsync(409, "stale-position");
        }
    }
}
