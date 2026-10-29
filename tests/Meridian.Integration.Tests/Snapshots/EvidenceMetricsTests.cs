using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>ADR 0007: every result carries completeness, abstention rate, missing data and a band, derived from the stored lines.</summary>
public sealed class EvidenceMetricsTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object June2020 = new { from = "2020-06-01", to = "2020-06-30" };

    /// <summary>
    /// Ten yes-positions in June 2020. FRA: nine matches and a miss (High, score 90). BRA: four matches, no data for the rest
    /// (Low, score 100). ARG: eight matches and two abstentions (Medium, score 100). JPN: absent in all ten (no band).
    /// ISL was not a member then.
    /// </summary>
    private static async Task<List<Data.Entities.Vote>> ArrangeAsync(Meridian.Fixtures.Fixtures fx)
    {
        var votes = new List<Data.Entities.Vote>();
        for (var i = 1; i <= 10; i++)
        {
            var vote = await fx.VoteAsync($"MA/2020/{i:000}", new DateOnly(2020, 6, i));
            await fx.PositionAsync(vote, Stance.Yes, 1);
            var ballots = new List<(string, Ballot)> { ("FRA", i == 10 ? N : Y), ("JPN", X), ("ISL", Y), ("ARG", i <= 8 ? Y : A) };
            if (i <= 4) ballots.Add(("BRA", Y));
            await fx.CountryVotesAsync(vote, [.. ballots]);
            votes.Add(vote);
        }
        return votes;
    }

    private static async Task<JsonElement> CalculatedAsync(ApiFactory api, string user, string? ruleSet)
    {
        var response = await api.ClientAs(user).PostAsJsonAsync("/api/snapshots",
            new { name = "June 2020", selection = June2020, ruleSet }, Ct);
        Assert.Equal(202, (int)response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("snapshotId").GetInt64();
        await SnapshotCalculationTests.RunJobsAsync(api.ConnectionString);
        return await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{id}", Ct);
    }

    private static JsonElement State(JsonElement snapshot, string code) =>
        snapshot.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("stateCode").GetString() == code);

    private static (double?, double?, double?, string?) Evidence(JsonElement result)
    {
        double? Number(string name) => result.GetProperty(name).ValueKind == JsonValueKind.Null ? null : result.GetProperty(name).GetDouble();
        return (Number("completeness"), Number("abstentionRate"), Number("missingData"),
            result.GetProperty("confidence").ValueKind == JsonValueKind.Null ? null : result.GetProperty("confidence").GetString());
    }

    [Fact]
    public async Task Each_result_carries_its_metrics_and_band_and_confidence_never_changes_the_rank()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var snapshot = await CalculatedAsync(api, "dana", ruleSet: null);

        Assert.Equal((1.0, 0.0, 0.0, "high"), Evidence(State(snapshot, "FRA")));
        Assert.Equal((0.4, 0.0, 0.6, "low"), Evidence(State(snapshot, "BRA")));
        Assert.Equal((0.8, 0.2, 0.0, "medium"), Evidence(State(snapshot, "ARG")));
        Assert.Equal((0.0, 0.0, 0.0, null), Evidence(State(snapshot, "JPN")));
        Assert.Equal((null, null, null, null), Evidence(State(snapshot, "ISL")));
        // BRA (Low) and ARG (Medium) score 100 and share rank 1, ahead of FRA (High) on 90: confidence is shown, never ranked on.
        Assert.Equal((1, 1), (State(snapshot, "BRA").GetProperty("rank").GetInt32(), State(snapshot, "ARG").GetProperty("rank").GetInt32()));
        Assert.Equal((90.0, 3), (State(snapshot, "FRA").GetProperty("score").GetDouble(), State(snapshot, "FRA").GetProperty("rank").GetInt32()));
    }

    [Fact]
    public async Task The_metrics_and_band_are_the_same_under_every_rule_set()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);

        var byRules = new List<(string, double?, double?, double?, string?)[]>();
        foreach (var ruleSet in new[] { "alignment-2026.1", "abstain-as-miss", "abstain-half-credit", "strict" })
        {
            var snapshot = await CalculatedAsync(api, "lena", ruleSet);
            byRules.Add(snapshot.GetProperty("results").EnumerateArray()
                .Select(r => (r.GetProperty("stateCode").GetString()!, Evidence(r).Item1, Evidence(r).Item2, Evidence(r).Item3, Evidence(r).Item4))
                .OrderBy(r => r.Item1, StringComparer.Ordinal).ToArray());
        }

        Assert.All(byRules.Skip(1), other => Assert.Equal(byRules[0], other));
    }

    [Fact]
    public async Task A_snapshot_stored_before_the_metrics_existed_has_them_derived_from_its_stored_lines()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var votes = await ArrangeAsync(fx);
        var stored = await fx.SnapshotAsync("Stored earlier", votes);

        var old = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/snapshots/{stored.Id}", Ct);
        var fresh = await CalculatedAsync(api, "dana", ruleSet: null);

        foreach (var code in new[] { "FRA", "BRA", "ARG", "JPN", "ISL" })
            Assert.Equal(Evidence(State(fresh, code)), Evidence(State(old, code)));
        Assert.Equal("fixture", old.GetProperty("scoringRulesVersion").GetString());
    }

    [Fact]
    public async Task A_states_breakdown_carries_the_same_metrics_as_its_ranking_row()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeAsync(fx);
        var snapshot = await CalculatedAsync(api, "dana", ruleSet: null);

        var breakdown = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>(
            $"/api/snapshots/{snapshot.GetProperty("id").GetInt64()}/states/ARG", Ct);

        Assert.Equal(Evidence(State(snapshot, "ARG")), Evidence(breakdown.GetProperty("result")));
    }
}
