using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>ADR 0007: the comparison reasons by each snapshot's own rule set and warns when they differ.</summary>
public sealed class ComparisonRuleSetTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object Mid2020 = new { from = "2020-06-01", to = "2020-07-31" };

    /// <summary>The same two votes as the rule set tests: weight 3 (yes) and weight 2 (no), with abstentions and absences.</summary>
    private static async Task ArrangeAsync(Meridian.Fixtures.Fixtures fx)
    {
        var first = await fx.VoteAsync("MA/2020/001", new DateOnly(2020, 6, 1));
        await fx.CountryVotesAsync(first, ("FRA", Y), ("BRA", N), ("ARG", A), ("JPN", X));
        await fx.PositionAsync(first, Stance.Yes, 3);
        var second = await fx.VoteAsync("MA/2020/002", new DateOnly(2020, 7, 1));
        await fx.CountryVotesAsync(second, ("FRA", N), ("BRA", A), ("ARG", Y), ("JPN", A));
        await fx.PositionAsync(second, Stance.No, 2);
    }

    private static async Task<long> CalculatedAsync(ApiFactory api, string user, string? ruleSet)
    {
        var response = await api.ClientAs(user).PostAsJsonAsync("/api/snapshots",
            new { name = ruleSet ?? "default", selection = Mid2020, ruleSet }, Ct);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("snapshotId").GetInt64();
        await SnapshotCalculationTests.RunJobsAsync(api.ConnectionString);
        return id;
    }

    private static Task<JsonElement> CompareAsync(ApiFactory api, long a, long b) =>
        api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/compare?a={a}&b={b}", Ct);

    private static JsonElement State(JsonElement comparison, string code) =>
        comparison.GetProperty("states").EnumerateArray().Single(s => s.GetProperty("stateCode").GetString() == code);

    private static (int, int, int) Basis(JsonElement state, string side) =>
        (state.GetProperty(side).GetProperty("countableVotes").GetInt32(), state.GetProperty(side).GetProperty("matchedWeight").GetInt32(),
            state.GetProperty(side).GetProperty("countedWeight").GetInt32());

    [Fact]
    public async Task The_comparison_names_both_rule_sets_and_says_they_differ()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);
        var (a, b) = (await CalculatedAsync(api, "dana", null), await CalculatedAsync(api, "lena", "strict"));

        var comparison = await CompareAsync(api, a, b);

        Assert.Equal(("alignment-2026.1", "strict", true),
            (comparison.GetProperty("a").GetProperty("ruleSet").GetProperty("id").GetString(),
                comparison.GetProperty("b").GetProperty("ruleSet").GetProperty("id").GetString(),
                comparison.GetProperty("ruleSetsDiffer").GetBoolean()));
        Assert.Equal("Strict", comparison.GetProperty("b").GetProperty("ruleSet").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Two_snapshots_under_the_same_rule_set_do_not_differ_and_have_no_reasons_when_nothing_moved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);
        var (a, b) = (await CalculatedAsync(api, "lena", "strict"), await CalculatedAsync(api, "lena", "strict"));

        var comparison = await CompareAsync(api, a, b);

        Assert.False(comparison.GetProperty("ruleSetsDiffer").GetBoolean());
        Assert.All(comparison.GetProperty("states").EnumerateArray(), s => Assert.Empty(s.GetProperty("reasons").EnumerateArray()));
    }

    [Fact]
    public async Task An_abstention_that_counts_under_one_rule_set_and_not_the_other_is_a_reason_of_its_own()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);
        var (a, b) = (await CalculatedAsync(api, "dana", null), await CalculatedAsync(api, "lena", "strict"));

        var comparison = await CompareAsync(api, a, b);

        // BRA abstained on the second vote (weight 2): out under the default, a miss under strict.
        var bra = State(comparison, "BRA");
        var reason = Assert.Single(bra.GetProperty("reasons").EnumerateArray());
        Assert.Equal(("MA/2020/002", "changed", 0, 2), (reason.GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            reason.GetProperty("kind").GetString(), reason.GetProperty("matchedChange").GetInt32(), reason.GetProperty("countedChange").GetInt32()));
        Assert.Equal(["rules"], reason.GetProperty("changes").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(("abstain", "abstain"), (reason.GetProperty("a").GetProperty("category").GetString(), reason.GetProperty("b").GetProperty("category").GetString()));
        Assert.Equal(((1, 0, 3), (1, 0, 5)), (Basis(bra, "basisA"), Basis(bra, "basisB")));
        // FRA voted yes and no, so strict changes nothing for it; JPN was absent and then abstained: 3 + 2 more counted.
        Assert.Empty(State(comparison, "FRA").GetProperty("reasons").EnumerateArray());
        Assert.Equal([3, 2], State(comparison, "JPN").GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("countedChange").GetInt32()));
    }

    [Fact]
    public async Task Weights_are_shown_in_the_finest_unit_of_the_two_rule_sets()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);
        var (a, b) = (await CalculatedAsync(api, "dana", null), await CalculatedAsync(api, "lena", "abstain-half-credit"));

        var comparison = await CompareAsync(api, a, b);

        Assert.Equal(2, comparison.GetProperty("weightUnit").GetInt32());
        // BRA under the default: 0 of 3 weight = 0 of 6 half points; under half credit: the abstention on weight 2 adds 2 of 4.
        var bra = State(comparison, "BRA");
        Assert.Equal(((1, 0, 6), (1, 2, 10)), (Basis(bra, "basisA"), Basis(bra, "basisB")));
        var reason = Assert.Single(bra.GetProperty("reasons").EnumerateArray());
        Assert.Equal((2, 4), (reason.GetProperty("matchedChange").GetInt32(), reason.GetProperty("countedChange").GetInt32()));
        // FRA has only yes and no ballots, so it just doubles.
        Assert.Equal(((2, 5 * 2, 5 * 2), (2, 10, 10)), (Basis(State(comparison, "FRA"), "basisA"), Basis(State(comparison, "FRA"), "basisB")));
    }
}
