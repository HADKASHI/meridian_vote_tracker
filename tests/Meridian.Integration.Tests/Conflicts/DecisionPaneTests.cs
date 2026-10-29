using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Conflicts;

/// <summary>The decision pane: GET /api/conflicts/{id} shows both values with their origins (CON-02; stories 52, 63).</summary>
public sealed class DecisionPaneTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_pending_conflict_shows_the_current_value_from_its_import_row_and_the_incoming_value_from_its_first_row()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var original = await fx.ImportAsync(uploadedBy: "dana", fileName: "q1.csv");
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1), origin: original);
        var approved = (await fx.CountryVotesAsync(vote, original, ("BRA", Y)))[0];
        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "q1-rev2.csv");
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A, openedBy: rev2);

        var pane = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);

        Assert.Equal(("MA/2024/101", "ballot", "BRA"), (pane.GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            pane.GetProperty("subject").GetString(), pane.GetProperty("stateCode").GetString()));
        Assert.Equal(approved.Id, pane.GetProperty("comparedRevisionId").GetInt64());
        var current = pane.GetProperty("current");
        Assert.Equal(("y", approved.Id), (current.GetProperty("value").GetProperty("ballot").GetString(),
            current.GetProperty("revisionId").GetInt64()));
        AssertImportRowOrigin(current.GetProperty("origin"), original.Id, "q1.csv", "dana");
        var incoming = pane.GetProperty("incoming");
        Assert.Equal("a", incoming.GetProperty("value").GetProperty("ballot").GetString());
        Assert.Equal(JsonValueKind.Null, incoming.GetProperty("revisionId").ValueKind);
        AssertImportRowOrigin(incoming.GetProperty("origin"), rev2.Id, "q1-rev2.csv", "omar");
        Assert.Equal(JsonValueKind.Null, pane.GetProperty("decision").ValueKind);
        Assert.Empty(pane.GetProperty("notApplied").EnumerateArray());
    }

    [Fact]
    public async Task A_current_value_approved_by_a_decision_shows_that_decision_as_its_origin()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var first = await fx.BallotConflictAsync(vote, "BRA", A);
        var second = await fx.BallotConflictAsync(vote, "BRA", N);
        var decision = await fx.DecideAsync(first, Decision.Accept, decidedBy: "omar");

        var pane = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/conflicts/{second.Id}", Ct);

        var origin = pane.GetProperty("current").GetProperty("origin");
        Assert.Equal(("decision", decision.Id, first.Id, "omar"), (origin.GetProperty("kind").GetString(),
            origin.GetProperty("decisionId").GetInt64(), origin.GetProperty("conflictId").GetInt64(),
            origin.GetProperty("decidedBy").GetString()));
        Assert.Equal("a", pane.GetProperty("current").GetProperty("value").GetProperty("ballot").GetString());
    }

    [Fact]
    public async Task A_decided_details_conflict_stays_viewable_with_its_decision_and_not_applied_attempts()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/007", new DateOnly(2024, 3, 1), session: 79, title: "Ocean Treaty");
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2024, 3, 2));
        var decision = await fx.DecideAsync(conflict, Decision.Accept, decidedBy: "dana", comment: "Assembly corrected the date");
        await fx.NotAppliedAsync(conflict, Decision.Reject, attemptedBy: "omar");

        var pane = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);

        var decided = pane.GetProperty("decision");
        Assert.Equal((decision.Id, "accept", "dana", "Assembly corrected the date"), (decided.GetProperty("id").GetInt64(),
            decided.GetProperty("decision").GetString(), decided.GetProperty("decidedBy").GetString(),
            decided.GetProperty("comment").GetString()));
        Assert.Equal(("2024-03-01", "2024-03-02"), (decided.GetProperty("from").GetProperty("voteDate").GetString(),
            decided.GetProperty("to").GetProperty("voteDate").GetString()));

        await using var db = new Meridian.Data.MeridianDbContext(Meridian.Data.MeridianDb.Options(api.ConnectionString));
        var created = await db.VoteDetailsRevisions.SingleAsync(r => r.OriginDecisionId == decision.Id, Ct);
        Assert.Equal(created.Id, pane.GetProperty("incoming").GetProperty("revisionId").GetInt64());
        Assert.Equal("decision", pane.GetProperty("current").GetProperty("origin").GetProperty("kind").GetString());

        var attempt = Assert.Single(pane.GetProperty("notApplied").EnumerateArray());
        Assert.Equal(("omar", "reject"), (attempt.GetProperty("attemptedBy").GetString(), attempt.GetProperty("attempted").GetString()));
    }

    [Fact]
    public async Task An_unknown_conflict_is_404_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/conflicts/999999", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }

    private static void AssertImportRowOrigin(JsonElement origin, long importId, string fileName, string uploadedBy)
    {
        Assert.Equal(("import_row", importId, fileName, uploadedBy), (origin.GetProperty("kind").GetString(),
            origin.GetProperty("importId").GetInt64(), origin.GetProperty("fileName").GetString(),
            origin.GetProperty("uploadedBy").GetString()));
        Assert.True(origin.GetProperty("lineNo").GetInt32() > 1);
    }
}
