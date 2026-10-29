using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;
using static Meridian.Integration.Tests.Snapshots.SnapshotCalculationTests;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>Extra (story 103): what would differ if a snapshot were created now, read from its stored revisions.</summary>
public sealed class SnapshotDriftTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<JsonElement> ChangesAsync(ApiFactory api, long snapshotId, string user = "dana") =>
        api.ClientAs(user).GetFromJsonAsync<JsonElement>($"/api/snapshots/{snapshotId}/changes", Ct);

    [Fact]
    public async Task A_changed_position_is_listed_with_the_old_and_new_value_and_who_set_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var changed = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(changed, Stance.Yes, 2);
        var same = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11));
        await fx.PositionAsync(same, Stance.No, 3);
        var untouched = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 1, 12));
        await fx.PositionAsync(untouched, Stance.Yes, 1);
        var snapshot = await fx.SnapshotAsync("Q1", [changed, same, untouched]);
        var before = await api.ClientAs("dana").GetStringAsync($"/api/snapshots/{snapshot.Id}", Ct);

        await fx.PositionAsync(changed, Stance.No, 5, setBy: "lena");
        // Setting the same stance and weight again is a new revision but no change.
        await fx.PositionAsync(same, Stance.No, 3, setBy: "lena");

        var drift = await ChangesAsync(api, snapshot.Id);

        var positions = drift.GetProperty("positionChanged");
        Assert.Equal(1, positions.GetProperty("count").GetInt32());
        var entry = Assert.Single(positions.GetProperty("votes").EnumerateArray());
        Assert.Equal("MA/2024/001", entry.GetProperty("vote").GetProperty("assemblyVoteId").GetString());
        Assert.Equal(("yes", 2), (entry.GetProperty("old").GetProperty("stance").GetString(), entry.GetProperty("old").GetProperty("weight").GetInt32()));
        Assert.Equal(("no", 5), (entry.GetProperty("new").GetProperty("stance").GetString(), entry.GetProperty("new").GetProperty("weight").GetInt32()));
        Assert.Equal(("position", "lena"), (entry.GetProperty("changedBy").GetProperty("kind").GetString(), entry.GetProperty("changedBy").GetProperty("by").GetString()));
        Assert.NotEqual(JsonValueKind.Null, entry.GetProperty("at").ValueKind);
        Assert.Equal(0, drift.GetProperty("detailsChanged").GetProperty("count").GetInt32());

        // Live, read now, with no score or rank, and the stored snapshot is untouched.
        Assert.True(DateTimeOffset.UtcNow - drift.GetProperty("readAt").GetDateTimeOffset() < TimeSpan.FromMinutes(1));
        var raw = drift.GetRawText();
        Assert.DoesNotContain("score", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rank", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await api.ClientAs("dana").GetStringAsync($"/api/snapshots/{snapshot.Id}", Ct));
    }

    [Fact]
    public async Task Changed_vote_details_show_the_old_and_new_value_and_the_decision_that_approved_them()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10), session: 79, title: "Ocean Treaty");
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var snapshot = await fx.SnapshotAsync("Q1", [vote]);
        var conflict = await fx.DetailsConflictAsync(vote, new DateOnly(2024, 1, 12), title: "Ocean Treaty (revised)");
        // A pending conflict is not a change.
        Assert.Equal(0, (await ChangesAsync(api, snapshot.Id)).GetProperty("detailsChanged").GetProperty("count").GetInt32());

        var decision = await fx.DecideAsync(conflict, Decision.Accept, decidedBy: "omar");

        var details = (await ChangesAsync(api, snapshot.Id)).GetProperty("detailsChanged");
        Assert.Equal(1, details.GetProperty("count").GetInt32());
        var entry = Assert.Single(details.GetProperty("votes").EnumerateArray());
        Assert.Equal(("Ocean Treaty", "2024-01-10", 79), (entry.GetProperty("old").GetProperty("title").GetString(),
            entry.GetProperty("old").GetProperty("voteDate").GetString(), entry.GetProperty("old").GetProperty("session").GetInt32()));
        Assert.Equal(("Ocean Treaty (revised)", "2024-01-12"), (entry.GetProperty("new").GetProperty("title").GetString(),
            entry.GetProperty("new").GetProperty("voteDate").GetString()));
        var origin = entry.GetProperty("changedBy");
        Assert.Equal(("decision", "omar", decision.Id), (origin.GetProperty("kind").GetString(), origin.GetProperty("by").GetString(),
            origin.GetProperty("decisionId").GetInt64()));
    }

    [Fact]
    public async Task Each_kind_gives_its_real_count_and_the_first_twenty_votes()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var votes = new List<Meridian.Data.Entities.Vote>();
        for (var i = 1; i <= 21; i++)
        {
            var vote = await fx.VoteAsync($"MA/2024/{i:000}", new DateOnly(2024, 1, 10));
            await fx.PositionAsync(vote, Stance.Yes, 1);
            votes.Add(vote);
        }
        var snapshot = await fx.SnapshotAsync("Many", votes);
        foreach (var vote in votes) await fx.PositionAsync(vote, Stance.No, 1);

        var positions = (await ChangesAsync(api, snapshot.Id)).GetProperty("positionChanged");

        Assert.Equal(21, positions.GetProperty("count").GetInt32());
        var listed = positions.GetProperty("votes").EnumerateArray().Select(v => v.GetProperty("vote").GetProperty("assemblyVoteId").GetString()).ToList();
        Assert.Equal(20, listed.Count);
        Assert.Equal(votes.Take(20).Select(v => v.AssemblyVoteId), listed);
    }

    [Fact]
    public async Task Unknown_or_uncalculated_snapshots_are_refused_and_nobody_may_ask()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(vote, Stance.Yes, 1);
        var done = await fx.SnapshotAsync("Done", [vote]);
        var queued = await CreateAsync(api, new { voteIds = new[] { vote.Id } }, "Still queued");

        await (await api.ClientAs("dana").GetAsync("/api/snapshots/999999/changes", Ct)).AssertProblemAsync(404, "not-found");
        await (await api.ClientAs("dana").GetAsync($"/api/snapshots/{queued}/changes", Ct)).AssertProblemAsync(400, "bad-request");
        Assert.Equal(401, (int)(await api.ClientAs(null).GetAsync($"/api/snapshots/{done.Id}/changes", Ct)).StatusCode);
        Assert.Equal(200, (int)(await api.ClientAs("lena").GetAsync($"/api/snapshots/{done.Id}/changes", Ct)).StatusCode);
    }
}
