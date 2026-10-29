using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Conflicts;

/// <summary>Exactly one of two simultaneous decisions applies; the loser sees the winner's (CON-05; stories 57, 58).</summary>
public sealed class DecisionRaceTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private const int Rounds = 20;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deciding_a_decided_conflict_gets_409_already_decided_with_the_winning_decision_and_is_recorded_as_not_applied()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);
        await api.ClientAs("dana").PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision",
            new { decision = "accept", comment = "Assembly confirmed" }, Ct);

        var response = await api.ClientAs("omar").PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision",
            new { decision = "reject" }, Ct);

        var problem = await response.AssertProblemAsync(409, "already-decided");
        var winner = problem.GetProperty("decision");
        Assert.Equal(("dana", "accept", "Assembly confirmed", "y", "a"), (
            winner.GetProperty("decidedBy").GetString(), winner.GetProperty("decision").GetString(),
            winner.GetProperty("comment").GetString(), winner.GetProperty("from").GetProperty("ballot").GetString(),
            winner.GetProperty("to").GetProperty("ballot").GetString()));
        Assert.True(winner.TryGetProperty("decidedAt", out _));

        var pane = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);
        var attempt = Assert.Single(pane.GetProperty("notApplied").EnumerateArray());
        Assert.Equal(("omar", "reject"), (attempt.GetProperty("attemptedBy").GetString(), attempt.GetProperty("attempted").GetString()));
        Assert.Equal("a", pane.GetProperty("current").GetProperty("value").GetProperty("ballot").GetString());
    }

    [Fact]
    public async Task Of_two_simultaneous_decisions_exactly_one_applies_in_every_round()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        string[] users = ["dana", "omar"];
        string[] decisions = ["accept", "reject"];
        var clients = users.Select(api.ClientAs).ToArray();

        for (var round = 0; round < Rounds; round++)
        {
            var state = round % 2 == 0 ? "BRA" : "FRA";
            await fx.CountryVotesAsync(vote, (state, round % 3 == 0 ? Y : N));
            var conflict = await fx.BallotConflictAsync(vote, state, A);

            var responses = await RaceBarrier.RunTogetherAsync(2, i =>
                clients[i].PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision", new { decision = decisions[i] }, Ct));

            var statuses = responses.Select(r => (int)r.StatusCode).Order().ToArray();
            Assert.True(statuses is [200, 409], $"Round {round}: {string.Join(", ", statuses)}");
            var winner = Array.FindIndex(responses, r => (int)r.StatusCode == 200);
            var problem = await responses[1 - winner].AssertProblemAsync(409, "already-decided");
            Assert.Equal(users[winner], problem.GetProperty("decision").GetProperty("decidedBy").GetString());

            await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
            var decided = await db.ConflictDecisions.SingleAsync(d => d.ConflictId == conflict.Id, Ct);
            Assert.Equal((users[winner], decisions[winner] == "accept" ? Decision.Accept : Decision.Reject),
                (decided.DecidedBy, decided.Decision));
            var notApplied = await db.DecisionAttemptsNotApplied.SingleAsync(a => a.ConflictId == conflict.Id, Ct);
            Assert.Equal(users[1 - winner], notApplied.AttemptedBy);
        }
    }
}
