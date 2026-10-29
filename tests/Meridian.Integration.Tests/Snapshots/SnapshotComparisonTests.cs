using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;
using static Meridian.Integration.Tests.Snapshots.SnapshotCalculationTests;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>Extra (story 102): two snapshots compared from what they stored.</summary>
public sealed class SnapshotComparisonTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<JsonElement> CompareAsync(ApiFactory api, long a, long b, string user = "dana") =>
        api.ClientAs(user).GetFromJsonAsync<JsonElement>($"/api/snapshots/compare?a={a}&b={b}", Ct);

    private static JsonElement State(JsonElement comparison, string code) =>
        comparison.GetProperty("states").EnumerateArray().Single(s => s.GetProperty("stateCode").GetString() == code);

    private static (int Votes, int Matched, int Counted) Basis(JsonElement state, string side)
    {
        var basis = state.GetProperty(side);
        return (basis.GetProperty("countableVotes").GetInt32(), basis.GetProperty("matchedWeight").GetInt32(),
            basis.GetProperty("countedWeight").GetInt32());
    }

    private static List<JsonElement> Reasons(JsonElement state) => state.GetProperty("reasons").EnumerateArray().ToList();

    private static string?[] Strings(JsonElement list) => list.EnumerateArray().Select(e => e.GetString()).ToArray();

    private static double? Number(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDouble();

    [Fact]
    public async Task Each_state_shows_its_score_and_rank_in_both_snapshots_and_how_far_it_moved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var q1 = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(q1, ("ARG", Y), ("BRA", Y), ("FRA", N));
        await fx.PositionAsync(q1, Stance.Yes, 1);
        var q2 = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 4, 10));
        await fx.CountryVotesAsync(q2, ("ARG", N), ("BRA", Y), ("FRA", Y));
        await fx.PositionAsync(q2, Stance.Yes, 1);
        var a = await fx.SnapshotAsync("Q1", [q1]);
        var b = await fx.SnapshotAsync("Q1 and Q2", [q1, q2]);

        var comparison = await CompareAsync(api, a.Id, b.Id);

        Assert.Equal(("Q1", "Q1 and Q2"), (comparison.GetProperty("a").GetProperty("name").GetString(),
            comparison.GetProperty("b").GetProperty("name").GetString()));
        // A: ARG 100 and BRA 100 share rank 1, FRA 0 is 3. B: BRA 100 is 1, ARG 50 and FRA 50 share 2.
        var arg = State(comparison, "ARG");
        Assert.Equal((100.0, 1, 50.0, 2, -50.0, -1), (Number(arg.GetProperty("scoreA")), arg.GetProperty("rankA").GetInt32(),
            Number(arg.GetProperty("scoreB")), arg.GetProperty("rankB").GetInt32(),
            Number(arg.GetProperty("scoreChange")), arg.GetProperty("rankChange").GetInt32()));
        var fra = State(comparison, "FRA");
        Assert.Equal((0.0, 3, 50.0, 2, 50.0, 1), (Number(fra.GetProperty("scoreA")), fra.GetProperty("rankA").GetInt32(),
            Number(fra.GetProperty("scoreB")), fra.GetProperty("rankB").GetInt32(),
            Number(fra.GetProperty("scoreChange")), fra.GetProperty("rankChange").GetInt32()));
        // ARG's basis: one countable vote in A (weight 1 of 1 matched); two in B (1 of 2).
        Assert.Equal((1, 1, 1), Basis(arg, "basisA"));
        Assert.Equal((2, 1, 2), Basis(arg, "basisB"));
        var bra = State(comparison, "BRA");
        Assert.Equal((0.0, 0), (Number(bra.GetProperty("scoreChange")), bra.GetProperty("rankChange").GetInt32()));
    }

    [Fact]
    public async Task A_state_that_is_n_a_in_one_snapshot_has_no_score_rank_or_movement_there_and_is_not_made_up_as_zero()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var first = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(first, ("FRA", Y), ("CIV", A));
        await fx.PositionAsync(first, Stance.Yes, 2);
        var second = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 10));
        await fx.CountryVotesAsync(second, ("ARG", Y), ("CIV", X));
        await fx.PositionAsync(second, Stance.Yes, 2);
        var a = await fx.SnapshotAsync("France only", [first]);
        var b = await fx.SnapshotAsync("Argentina only", [second]);

        var comparison = await CompareAsync(api, a.Id, b.Id);

        var fra = State(comparison, "FRA");
        Assert.Equal((100.0, 1), (Number(fra.GetProperty("scoreA")), fra.GetProperty("rankA").GetInt32()));
        Assert.All(new[] { "scoreB", "rankB", "scoreChange", "rankChange" },
            name => Assert.Equal(JsonValueKind.Null, fra.GetProperty(name).ValueKind));
        var arg = State(comparison, "ARG");
        Assert.Equal(JsonValueKind.Null, arg.GetProperty("scoreA").ValueKind);
        Assert.Equal(JsonValueKind.Null, arg.GetProperty("rankA").ValueKind);
        Assert.Equal(JsonValueKind.Null, arg.GetProperty("scoreChange").ValueKind);
        Assert.Equal(JsonValueKind.Null, arg.GetProperty("rankChange").ValueKind);
        // CIV only abstained or was absent: n/a in both, still listed, with no movement.
        var civ = State(comparison, "CIV");
        Assert.All(new[] { "scoreA", "rankA", "scoreB", "rankB", "scoreChange", "rankChange" },
            name => Assert.Equal(JsonValueKind.Null, civ.GetProperty(name).ValueKind));
        // Ranked in B first, then the states with no rank in B by code.
        var order = comparison.GetProperty("states").EnumerateArray().Select(s => s.GetProperty("stateCode").GetString()).ToList();
        Assert.Equal("ARG", order[0]);
        Assert.Equal(order.Skip(1).Order(StringComparer.Ordinal), order.Skip(1));
    }

    [Fact]
    public async Task The_comparison_reads_stored_results_so_later_changes_to_the_data_do_not_move_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("ARG", Y), ("BRA", N));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var a = await fx.SnapshotAsync("Before", [vote]);
        await fx.PositionAsync(vote, Stance.Yes, 3);
        var b = await fx.SnapshotAsync("Heavier", [vote]);
        var before = await CompareAsync(api, a.Id, b.Id);
        Assert.Equal(1, Reasons(State(before, "ARG")).Single().GetProperty("matchedChange").GetInt32());

        await fx.DecideAsync(await fx.BallotConflictAsync(vote, "BRA", Y), Decision.Accept);
        await fx.PositionAsync(vote, Stance.No, 5);

        Assert.Equal(before.GetRawText(), (await CompareAsync(api, a.Id, b.Id)).GetRawText());
    }

    [Fact]
    public async Task Unknown_or_uncalculated_snapshots_are_refused_and_nobody_may_compare()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("ARG", Y));
        await fx.PositionAsync(vote, Stance.Yes, 1);
        var done = await fx.SnapshotAsync("Done", [vote]);
        var queued = await CreateAsync(api, new { voteIds = new[] { vote.Id } }, "Still queued");

        await (await api.ClientAs("dana").GetAsync($"/api/snapshots/compare?a={done.Id}&b=999999", Ct)).AssertProblemAsync(404, "not-found");
        await (await api.ClientAs("dana").GetAsync($"/api/snapshots/compare?a=999999&b={done.Id}", Ct)).AssertProblemAsync(404, "not-found");
        await (await api.ClientAs("dana").GetAsync($"/api/snapshots/compare?a={done.Id}&b={queued}", Ct)).AssertProblemAsync(400, "bad-request");
        Assert.Equal(401, (int)(await api.ClientAs(null).GetAsync($"/api/snapshots/compare?a={done.Id}&b={done.Id}", Ct)).StatusCode);
        Assert.Equal(200, (int)(await api.ClientAs("lena").GetAsync($"/api/snapshots/compare?a={done.Id}&b={done.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_ballot_changed_between_the_snapshots_is_the_reason_the_state_moved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10), title: "Ocean Treaty");
        await fx.CountryVotesAsync(vote, ("BRA", N), ("ARG", Y));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var a = await fx.SnapshotAsync("Before the decision", [vote]);
        await fx.DecideAsync(await fx.BallotConflictAsync(vote, "BRA", Y), Decision.Accept);
        var b = await fx.SnapshotAsync("After the decision", [vote]);

        var comparison = await CompareAsync(api, a.Id, b.Id);

        var bra = State(comparison, "BRA");
        var reason = Assert.Single(Reasons(bra));
        Assert.Equal(("MA/2024/001", "Ocean Treaty", "changed"), (reason.GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            reason.GetProperty("title").GetString(), reason.GetProperty("kind").GetString()));
        Assert.Equal(["category", "ballot"], Strings(reason.GetProperty("changes")));
        Assert.Equal(("mismatch", "n", "yes", 2), (reason.GetProperty("a").GetProperty("category").GetString(),
            reason.GetProperty("a").GetProperty("ballot").GetString(), reason.GetProperty("a").GetProperty("stance").GetString(),
            reason.GetProperty("a").GetProperty("weight").GetInt32()));
        Assert.Equal(("match", "y"), (reason.GetProperty("b").GetProperty("category").GetString(), reason.GetProperty("b").GetProperty("ballot").GetString()));
        // BRA went from 0 of 2 to 2 of 2: the reason carries exactly that change.
        Assert.Equal((2, 0), (reason.GetProperty("matchedChange").GetInt32(), reason.GetProperty("countedChange").GetInt32()));
        // ARG voted the same in both: no reasons, an empty list rather than null.
        Assert.Empty(Reasons(State(comparison, "ARG")));
    }

    [Fact]
    public async Task A_changed_position_is_the_reason_when_the_stance_flips_a_match_into_a_mismatch_and_the_weight_moves()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        await fx.PositionAsync(vote, Stance.Yes, 2);
        var a = await fx.SnapshotAsync("Yes, weight 2", [vote]);
        await fx.PositionAsync(vote, Stance.No, 5);
        var b = await fx.SnapshotAsync("No, weight 5", [vote]);

        var reason = Assert.Single(Reasons(State(await CompareAsync(api, a.Id, b.Id), "BRA")));

        Assert.Equal(["category", "stance", "weight"], Strings(reason.GetProperty("changes")));
        Assert.Equal(("match", "yes", 2, "mismatch", "no", 5), (reason.GetProperty("a").GetProperty("category").GetString(),
            reason.GetProperty("a").GetProperty("stance").GetString(), reason.GetProperty("a").GetProperty("weight").GetInt32(),
            reason.GetProperty("b").GetProperty("category").GetString(), reason.GetProperty("b").GetProperty("stance").GetString(),
            reason.GetProperty("b").GetProperty("weight").GetInt32()));
        Assert.Equal((-2, 3), (reason.GetProperty("matchedChange").GetInt32(), reason.GetProperty("countedChange").GetInt32()));
    }

    [Fact]
    public async Task A_vote_only_in_one_snapshot_is_a_reason_but_only_when_it_counted_for_the_state()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var shared = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(shared, ("BRA", Y), ("ARG", Y));
        await fx.PositionAsync(shared, Stance.Yes, 1);
        var added = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 10), title: "Space Debris");
        await fx.CountryVotesAsync(added, ("BRA", N), ("ARG", A));
        await fx.PositionAsync(added, Stance.Yes, 4);
        var a = await fx.SnapshotAsync("One vote", [shared]);
        var b = await fx.SnapshotAsync("Two votes", [shared, added]);

        var comparison = await CompareAsync(api, a.Id, b.Id);

        var reason = Assert.Single(Reasons(State(comparison, "BRA")));
        Assert.Equal(("only_in_b", "MA/2024/002", "Space Debris"), (reason.GetProperty("kind").GetString(),
            reason.GetProperty("vote").GetProperty("assemblyVoteId").GetString(), reason.GetProperty("title").GetString()));
        Assert.Equal(JsonValueKind.Null, reason.GetProperty("a").ValueKind);
        Assert.Equal(("mismatch", "n"), (reason.GetProperty("b").GetProperty("category").GetString(), reason.GetProperty("b").GetProperty("ballot").GetString()));
        Assert.Empty(reason.GetProperty("changes").EnumerateArray());
        Assert.Equal((0, 4), (reason.GetProperty("matchedChange").GetInt32(), reason.GetProperty("countedChange").GetInt32()));
        // ARG abstained on the added vote: it counted for nothing, so it explains nothing.
        Assert.Empty(Reasons(State(comparison, "ARG")));
    }

    [Fact]
    public async Task Every_states_reasons_add_up_to_its_change_in_matched_and_counted_weight()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var first = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(first, ("ARG", Y), ("BRA", N), ("FRA", Y), ("CIV", A));
        await fx.PositionAsync(first, Stance.Yes, 2);
        var second = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 10));
        await fx.CountryVotesAsync(second, ("ARG", N), ("BRA", N), ("FRA", Y), ("CIV", X));
        await fx.PositionAsync(second, Stance.No, 3);
        var a = await fx.SnapshotAsync("First vote", [first]);
        await fx.DecideAsync(await fx.BallotConflictAsync(first, "ARG", N), Decision.Accept);
        await fx.PositionAsync(first, Stance.Yes, 5);
        var b = await fx.SnapshotAsync("Both votes, revised", [first, second]);

        var comparison = await CompareAsync(api, a.Id, b.Id);

        foreach (var state in comparison.GetProperty("states").EnumerateArray())
        {
            var (basisA, basisB) = (state.GetProperty("basisA"), state.GetProperty("basisB"));
            var reasons = Reasons(state);
            Assert.Equal(basisB.GetProperty("matchedWeight").GetInt32() - basisA.GetProperty("matchedWeight").GetInt32(),
                reasons.Sum(r => r.GetProperty("matchedChange").GetInt32()));
            Assert.Equal(basisB.GetProperty("countedWeight").GetInt32() - basisA.GetProperty("countedWeight").GetInt32(),
                reasons.Sum(r => r.GetProperty("countedChange").GetInt32()));
        }
        // CIV abstained then was absent, FRA voted the same way on an unchanged vote: nothing to explain.
        Assert.Empty(Reasons(State(comparison, "CIV")));
    }

    [Fact]
    public async Task A_state_that_became_countable_lists_the_votes_that_made_it_so_and_reasons_come_in_vote_date_order()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var later = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 3, 1));
        await fx.CountryVotesAsync(later, ("BRA", Y));
        await fx.PositionAsync(later, Stance.Yes, 1);
        var earlier = await fx.VoteAsync("MA/2024/010", new DateOnly(2024, 1, 5));
        await fx.CountryVotesAsync(earlier, ("BRA", Y));
        await fx.PositionAsync(earlier, Stance.Yes, 1);
        var unrelated = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(unrelated, ("ARG", Y));
        await fx.PositionAsync(unrelated, Stance.Yes, 1);
        var a = await fx.SnapshotAsync("Nothing for BRA", [unrelated]);
        var b = await fx.SnapshotAsync("BRA votes added", [later, earlier]);

        var bra = State(await CompareAsync(api, a.Id, b.Id), "BRA");

        Assert.Null(Number(bra.GetProperty("scoreA")));
        Assert.Equal(100.0, Number(bra.GetProperty("scoreB")));
        // MA/2024/010 is the earlier vote even though its id sorts after MA/2024/002.
        Assert.Equal(["MA/2024/010", "MA/2024/002"], Reasons(bra).Select(r => r.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
    }
}
