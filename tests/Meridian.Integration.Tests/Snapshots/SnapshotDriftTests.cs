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

    [Fact]
    public async Task A_changed_country_vote_shows_the_old_and_new_ballot_and_what_approved_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("ARG", Y), ("BRA", N), ("FRA", Y));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var snapshot = await fx.SnapshotAsync("Q1", [vote]);
        var pending = await fx.BallotConflictAsync(vote, "BRA", Y);
        // A pending conflict is not a change.
        Assert.Equal(0, (await ChangesAsync(api, snapshot.Id)).GetProperty("countryVotesChanged").GetProperty("count").GetInt32());

        var decision = await fx.DecideAsync(pending, Decision.Accept, decidedBy: "omar");

        var changed = (await ChangesAsync(api, snapshot.Id)).GetProperty("countryVotesChanged");
        Assert.Equal(1, changed.GetProperty("count").GetInt32());
        var entry = Assert.Single(changed.GetProperty("votes").EnumerateArray());
        Assert.Equal(("MA/2024/001", "BRA", "n", "y"), (entry.GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            entry.GetProperty("stateCode").GetString(), entry.GetProperty("old").GetString(), entry.GetProperty("new").GetString()));
        var origin = entry.GetProperty("changedBy");
        Assert.Equal(("decision", "omar", decision.Id), (origin.GetProperty("kind").GetString(), origin.GetProperty("by").GetString(),
            origin.GetProperty("decisionId").GetInt64()));
    }

    [Fact]
    public async Task A_state_the_snapshot_had_no_data_for_that_now_has_a_ballot_is_listed_from_none_with_its_import()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("ARG", Y));
        await fx.PositionAsync(vote, Stance.Yes, 1);
        var snapshot = await fx.SnapshotAsync("Q1", [vote]);
        var later = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11));
        await fx.CountryVotesAsync(later, ("ARG", N));
        var late = await fx.ImportAsync(uploadedBy: "omar", fileName: "late.csv");
        await fx.CountryVotesAsync(vote, late, ("BRA", A));

        var changed = (await ChangesAsync(api, snapshot.Id)).GetProperty("countryVotesChanged");

        // ARG is unchanged, and MA/2024/002 is not in the snapshot.
        Assert.Equal(1, changed.GetProperty("count").GetInt32());
        var entry = Assert.Single(changed.GetProperty("votes").EnumerateArray());
        Assert.Equal("BRA", entry.GetProperty("stateCode").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("old").ValueKind);
        Assert.Equal("a", entry.GetProperty("new").GetString());
        var origin = entry.GetProperty("changedBy");
        Assert.Equal(("import", "omar", late.Id), (origin.GetProperty("kind").GetString(), origin.GetProperty("by").GetString(),
            origin.GetProperty("importId").GetInt64()));
    }

    [Fact]
    public async Task A_new_vote_in_the_date_range_is_listed_and_a_vote_moved_out_of_it_is_listed_with_why()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var stays = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        var moves = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 10), title: "Space Debris");
        var removed = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 2, 11));
        foreach (var vote in new[] { stays, moves, removed }) await fx.PositionAsync(vote, Stance.Yes, 1);
        var snapshotId = await CreateAsync(api, new
        {
            from = "2024-01-01", to = "2024-03-31", excludedVoteIds = new[] { removed.Id },
        });
        await RunJobsAsync(api.ConnectionString);

        var added = await fx.VoteAsync("MA/2024/004", new DateOnly(2024, 3, 1), session: 80, title: "Fisheries");
        await fx.PositionAsync(added, Stance.No, 4);
        var noPosition = await fx.VoteAsync("MA/2024/005", new DateOnly(2024, 3, 2));
        await fx.VoteAsync("MA/2024/006", new DateOnly(2024, 6, 1));
        await fx.PositionAsync(await fx.VoteAsync("MA/2024/007", new DateOnly(2023, 12, 31)), Stance.Yes, 1);
        var decision = await fx.DecideAsync(await fx.DetailsConflictAsync(moves, new DateOnly(2024, 4, 2)), Decision.Accept, decidedBy: "omar");

        var drift = await ChangesAsync(api, snapshotId);

        var now = drift.GetProperty("nowSelected");
        Assert.Equal(2, now.GetProperty("count").GetInt32());
        var listed = now.GetProperty("votes").EnumerateArray().ToList();
        Assert.Equal(["MA/2024/004", "MA/2024/005"], listed.Select(v => v.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal(("Fisheries", "2024-03-01", 80), (listed[0].GetProperty("details").GetProperty("title").GetString(),
            listed[0].GetProperty("details").GetProperty("voteDate").GetString(), listed[0].GetProperty("details").GetProperty("session").GetInt32()));
        Assert.Equal(("no", 4), (listed[0].GetProperty("position").GetProperty("stance").GetString(), listed[0].GetProperty("position").GetProperty("weight").GetInt32()));
        Assert.Equal(JsonValueKind.Null, listed[1].GetProperty("position").ValueKind);

        var left = drift.GetProperty("noLongerSelected");
        Assert.Equal(1, left.GetProperty("count").GetInt32());
        var entry = Assert.Single(left.GetProperty("votes").EnumerateArray());
        Assert.Equal("MA/2024/002", entry.GetProperty("vote").GetProperty("assemblyVoteId").GetString());
        Assert.Equal(("2024-02-10", "2024-04-02"), (entry.GetProperty("old").GetProperty("voteDate").GetString(), entry.GetProperty("new").GetProperty("voteDate").GetString()));
        Assert.Equal(("decision", "omar", decision.Id), (entry.GetProperty("changedBy").GetProperty("kind").GetString(),
            entry.GetProperty("changedBy").GetProperty("by").GetString(), entry.GetProperty("changedBy").GetProperty("decisionId").GetInt64()));
        Assert.Equal(noPosition.AssemblyVoteId, listed[1].GetProperty("vote").GetProperty("assemblyVoteId").GetString());
    }

    [Fact]
    public async Task Sessions_and_explicit_lists_are_resolved_like_the_preview_resolves_them()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var inSession = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10), session: 79);
        var leaves = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11), session: 79);
        await fx.PositionAsync(inSession, Stance.Yes, 1);
        await fx.PositionAsync(leaves, Stance.Yes, 1);
        var bySession = await CreateAsync(api, new { sessions = new[] { 79 } }, "By session");
        var byList = await CreateAsync(api, new { voteIds = new[] { inSession.Id, leaves.Id } }, "By list");
        await RunJobsAsync(api.ConnectionString);
        var joins = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 5, 1), session: 79);
        await fx.PositionAsync(joins, Stance.No, 1);
        await fx.DecideAsync(await fx.DetailsConflictAsync(leaves, session: 80), Decision.Accept);

        var session = await ChangesAsync(api, bySession);
        Assert.Equal(["MA/2024/003"], session.GetProperty("nowSelected").GetProperty("votes").EnumerateArray().Select(v => v.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal(["MA/2024/002"], session.GetProperty("noLongerSelected").GetProperty("votes").EnumerateArray().Select(v => v.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));

        // A list names its votes: a new vote never joins it, and a vote whose details changed still belongs to it.
        var list = await ChangesAsync(api, byList);
        Assert.Equal(0, list.GetProperty("nowSelected").GetProperty("count").GetInt32());
        Assert.Equal(0, list.GetProperty("noLongerSelected").GetProperty("count").GetInt32());
        Assert.Equal(1, list.GetProperty("detailsChanged").GetProperty("count").GetInt32());
    }
}
