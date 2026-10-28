using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Conflicts;

/// <summary>Accepting or rejecting a conflict: POST /api/conflicts/{id}/decision (CON-03; stories 53–56).</summary>
public sealed class DecisionTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accepting_a_ballot_conflict_approves_the_incoming_value_with_the_decision_as_origin_and_keeps_the_old_revision()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        var original = (await fx.CountryVotesAsync(vote, ("BRA", Y)))[0];
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);
        var client = api.ClientAs("dana");

        var response = await client.PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision",
            new { decision = "accept", comment = "Assembly confirmed the abstention" }, Ct);

        Assert.Equal(200, (int)response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(("accept", "dana", "Assembly confirmed the abstention", "y", "a"), (
            decision.GetProperty("decision").GetString(), decision.GetProperty("decidedBy").GetString(),
            decision.GetProperty("comment").GetString(), decision.GetProperty("from").GetProperty("ballot").GetString(),
            decision.GetProperty("to").GetProperty("ballot").GetString()));

        var pane = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);
        var current = pane.GetProperty("current");
        Assert.Equal("a", current.GetProperty("value").GetProperty("ballot").GetString());
        Assert.Equal(("decision", decision.GetProperty("id").GetInt64()),
            (current.GetProperty("origin").GetProperty("kind").GetString(), current.GetProperty("origin").GetProperty("decisionId").GetInt64()));
        Assert.Equal(current.GetProperty("revisionId").GetInt64(), pane.GetProperty("incoming").GetProperty("revisionId").GetInt64());

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal([original.Id, current.GetProperty("revisionId").GetInt64()],
            await db.CountryVoteRevisions.Where(r => r.VoteId == vote.Id).OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(Ct));
        Assert.Equal(original.Id,
            (await db.ConflictDecisions.SingleAsync(d => d.ConflictId == conflict.Id, Ct)).ReplacedCountryVoteRevisionId);
    }

    [Fact]
    public async Task Rejecting_a_conflict_changes_nothing_approved()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        var original = (await fx.CountryVotesAsync(vote, ("BRA", Y)))[0];
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);
        var client = api.ClientAs("omar");

        var response = await client.PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision", new { decision = "reject" }, Ct);

        Assert.Equal(200, (int)response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(("reject", JsonValueKind.Null), (decision.GetProperty("decision").GetString(), decision.GetProperty("comment").ValueKind));
        var pane = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);
        Assert.Equal(("y", original.Id, "import_row"), (
            pane.GetProperty("current").GetProperty("value").GetProperty("ballot").GetString(),
            pane.GetProperty("current").GetProperty("revisionId").GetInt64(),
            pane.GetProperty("current").GetProperty("origin").GetProperty("kind").GetString()));
        Assert.Equal(JsonValueKind.Null, pane.GetProperty("incoming").GetProperty("revisionId").ValueKind);
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(1, await db.CountryVoteRevisions.CountAsync(Ct));
    }

    [Fact]
    public async Task Accepting_a_vote_details_conflict_approves_the_incoming_details()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/007", new DateOnly(2024, 3, 1), session: 79, title: "Ocean Treaty");
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2024, 3, 2), title: "Ocean Treaty (amended)");
        var client = api.ClientAs("dana");

        var response = await client.PostAsJsonAsync($"/api/conflicts/{conflict.Id}/decision", new { decision = "accept" }, Ct);

        Assert.Equal(200, (int)response.StatusCode);
        var pane = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);
        var current = pane.GetProperty("current");
        Assert.Equal(("2024-03-02", "Ocean Treaty (amended)", 79, "decision"), (
            current.GetProperty("value").GetProperty("voteDate").GetString(),
            current.GetProperty("value").GetProperty("title").GetString(),
            current.GetProperty("value").GetProperty("session").GetInt32(),
            current.GetProperty("origin").GetProperty("kind").GetString()));
        Assert.Equal("2024-03-01", pane.GetProperty("decision").GetProperty("from").GetProperty("voteDate").GetString());
    }

    [Fact]
    public async Task A_conflict_is_decided_against_the_current_value_at_decision_time_and_deciding_one_never_rewrites_another()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        var original = (await fx.CountryVotesAsync(vote, ("BRA", Y)))[0];
        var toAbstain = await fx.BallotConflictAsync(vote, "BRA", A);
        var toNo = await fx.BallotConflictAsync(vote, "BRA", N);
        var client = api.ClientAs("dana");

        await client.PostAsJsonAsync($"/api/conflicts/{toAbstain.Id}/decision", new { decision = "accept" }, Ct);
        var response = await client.PostAsJsonAsync($"/api/conflicts/{toNo.Id}/decision", new { decision = "reject" }, Ct);

        var decision = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(("a", "n"), (decision.GetProperty("from").GetProperty("ballot").GetString(),
            decision.GetProperty("to").GetProperty("ballot").GetString()));
        var pane = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts/{toNo.Id}", Ct);
        Assert.Equal(original.Id, pane.GetProperty("comparedRevisionId").GetInt64());
        var first = await client.GetFromJsonAsync<JsonElement>($"/api/conflicts/{toAbstain.Id}", Ct);
        Assert.Equal(("y", "a"), (first.GetProperty("decision").GetProperty("from").GetProperty("ballot").GetString(),
            first.GetProperty("decision").GetProperty("to").GetProperty("ballot").GetString()));
    }

    [Theory]
    [InlineData("""{"comment":"no decision"}""", "decision")]
    [InlineData("""{"decision":"accept","comment":"COMMENT"}""", "comment")]
    public async Task An_invalid_decision_is_a_validation_problem_on_its_field(string body, string field)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);

        var response = await api.ClientAs("dana").PostAsync($"/api/conflicts/{conflict.Id}/decision",
            new StringContent(body.Replace("COMMENT", new string('x', 2001)), System.Text.Encoding.UTF8, "application/json"), Ct);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Deciding_an_unknown_conflict_is_404_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").PostAsJsonAsync("/api/conflicts/999999/decision", new { decision = "accept" }, Ct);

        await response.AssertProblemAsync(404, "not-found");
    }
}
