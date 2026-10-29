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
        var b = await fx.SnapshotAsync("Same data", [vote]);
        var before = (await CompareAsync(api, a.Id, b.Id)).GetRawText();

        await fx.DecideAsync(await fx.BallotConflictAsync(vote, "BRA", Y), Decision.Accept);
        await fx.PositionAsync(vote, Stance.No, 5);

        Assert.Equal(before, (await CompareAsync(api, a.Id, b.Id)).GetRawText());
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
}
