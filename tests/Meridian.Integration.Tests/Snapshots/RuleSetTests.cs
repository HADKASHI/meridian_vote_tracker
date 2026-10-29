using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>ADR 0007: a snapshot is scored under the rule set it was created with, and nothing else changes.</summary>
public sealed class RuleSetTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object Mid2020 = new { from = "2020-06-01", to = "2020-07-31" };

    /// <summary>
    /// Two votes in mid-2020, when Iceland is not a member. FRA yes/yes, BRA no then abstain, ARG abstain then wrong,
    /// JPN absent then abstain; ISL has ballots but no membership; the rest have no data.
    /// </summary>
    private static async Task ArrangeAsync(Meridian.Fixtures.Fixtures fx)
    {
        var first = await fx.VoteAsync("MA/2020/001", new DateOnly(2020, 6, 1));
        await fx.CountryVotesAsync(first, ("FRA", Y), ("BRA", N), ("ARG", A), ("JPN", X), ("ISL", Y));
        await fx.PositionAsync(first, Stance.Yes, 3);
        var second = await fx.VoteAsync("MA/2020/002", new DateOnly(2020, 7, 1));
        await fx.CountryVotesAsync(second, ("FRA", N), ("BRA", A), ("ARG", Y), ("JPN", A), ("ISL", N));
        await fx.PositionAsync(second, Stance.No, 2);
    }

    private static async Task<HttpResponseMessage> CreateAsync(ApiFactory api, string user, string? ruleSet) =>
        await api.ClientAs(user).PostAsJsonAsync("/api/snapshots",
            new { name = "Mid 2020", selection = Mid2020, ruleSet }, Ct);

    private static async Task<JsonElement> CalculatedAsync(ApiFactory api, string user, string? ruleSet)
    {
        var response = await CreateAsync(api, user, ruleSet);
        Assert.Equal(202, (int)response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("snapshotId").GetInt64();
        await SnapshotCalculationTests.RunJobsAsync(api.ConnectionString);
        return await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{id}", Ct);
    }

    private static (string, int, int, int, double?, int?)[] Scored(JsonElement snapshot) =>
        snapshot.GetProperty("results").EnumerateArray()
            .Where(r => r.GetProperty("stateCode").GetString() is "FRA" or "BRA" or "ARG" or "JPN" or "ISL")
            .Select(r => (r.GetProperty("stateCode").GetString()!, r.GetProperty("matchedWeight").GetInt32(),
                r.GetProperty("countedWeight").GetInt32(), r.GetProperty("countableVotes").GetInt32(),
                r.GetProperty("score").ValueKind == JsonValueKind.Null ? (double?)null : Math.Round(r.GetProperty("score").GetDouble(), 1),
                r.GetProperty("rank").ValueKind == JsonValueKind.Null ? (int?)null : r.GetProperty("rank").GetInt32()))
            .ToArray();

    [Fact]
    public async Task Without_a_choice_a_snapshot_is_scored_under_the_default_rule_set()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var snapshot = await CalculatedAsync(api, "dana", ruleSet: null);

        var ruleSet = snapshot.GetProperty("ruleSet");
        Assert.Equal(("alignment-2026.1", 1), (ruleSet.GetProperty("id").GetString(), ruleSet.GetProperty("weightUnit").GetInt32()));
        Assert.Equal("alignment-2026.1", snapshot.GetProperty("scoringRulesVersion").GetString());
        Assert.Equal(
        [
            ("FRA", 5, 5, 2, 100.0, 1), ("ARG", 0, 2, 1, 0.0, 2), ("BRA", 0, 3, 1, 0.0, 2),
            ("ISL", 0, 0, 0, null, null), ("JPN", 0, 0, 0, null, null),
        ], Scored(snapshot).OrderBy(r => r.Item6 is null).ThenBy(r => r.Item6).ThenBy(r => r.Item1, StringComparer.Ordinal));
    }

    [Theory]
    // abstain-as-miss: abstentions count as not aligned, absences are left out. JPN: absent (out), abstain (2 weight).
    [InlineData("abstain-as-miss", 1, "FRA:5:5:2:100:1", "ARG:0:5:1:0:2", "BRA:0:5:1:0:2", "JPN:0:2:0:0:2", "ISL:0:0:0::")]
    // abstain-half-credit: weights in half points. FRA 10/10, JPN abstains for 2 of 4, ARG 3 of 10, BRA 2 of 10.
    [InlineData("abstain-half-credit", 2, "FRA:10:10:2:100:1", "JPN:2:4:0:50:2", "ARG:3:10:1:30:3", "BRA:2:10:1:20:4", "ISL:0:0:0::")]
    // strict: the absence and both abstentions are misses.
    [InlineData("strict", 1, "FRA:5:5:2:100:1", "ARG:0:5:1:0:2", "BRA:0:5:1:0:2", "JPN:0:5:0:0:2", "ISL:0:0:0::")]
    public async Task The_admin_scores_a_snapshot_under_another_rule_set_in_that_rule_sets_weight_unit(
        string ruleSet, int unit, params string[] expected)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var snapshot = await CalculatedAsync(api, "lena", ruleSet);

        Assert.Equal((ruleSet, unit), (snapshot.GetProperty("ruleSet").GetProperty("id").GetString(),
            snapshot.GetProperty("ruleSet").GetProperty("weightUnit").GetInt32()));
        Assert.Equal(ruleSet, snapshot.GetProperty("scoringRulesVersion").GetString());
        var actual = Scored(snapshot).Select(r => $"{r.Item1}:{r.Item2}:{r.Item3}:{r.Item4}:{r.Item5}:{r.Item6}").ToArray();
        Assert.Equal(expected.OrderBy(e => e, StringComparer.Ordinal), actual.OrderBy(e => e, StringComparer.Ordinal));
    }

    [Fact]
    public async Task An_analyst_may_create_with_the_default_but_not_choose_another_rule_set()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var refused = await CreateAsync(api, "dana", "strict");
        await refused.AssertProblemAsync(403, "forbidden");
        var list = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/snapshots", Ct);
        Assert.Equal(0, list.GetProperty("total").GetInt32());

        var allowed = await CreateAsync(api, "dana", "alignment-2026.1");
        Assert.Equal(202, (int)allowed.StatusCode);
    }

    [Fact]
    public async Task An_unknown_rule_set_is_a_validation_problem_on_the_rule_set_field()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var problem = await (await CreateAsync(api, "lena", "alignment-2020")).AssertProblemAsync(400, "validation");

        Assert.True(problem.GetProperty("errors").TryGetProperty("ruleSet", out _));
    }

    [Fact]
    public async Task The_known_rule_sets_are_listed_with_their_names_and_weight_units()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var sets = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/rule-sets", Ct);

        Assert.Equal([("alignment-2026.1", 1, true), ("abstain-as-miss", 1, false), ("abstain-half-credit", 2, false), ("strict", 1, false)],
            sets.EnumerateArray().Select(s => (s.GetProperty("id").GetString()!, s.GetProperty("weightUnit").GetInt32(),
                s.GetProperty("isDefault").GetBoolean())));
    }
}
