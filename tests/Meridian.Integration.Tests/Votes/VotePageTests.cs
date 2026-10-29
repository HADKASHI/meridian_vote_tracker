using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Votes;

/// <summary>HIS-01, HIS-02, user stories 66 and 67: a vote's current state, each approved value with its origin.</summary>
public sealed class VotePageTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement CountryVote(JsonElement vote, string code) =>
        vote.GetProperty("countryVotes").EnumerateArray().Single(c => c.GetProperty("stateCode").GetString() == code);

    [Fact]
    public async Task The_page_shows_the_approved_details_position_and_pending_conflicts()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 15), session: 79, title: "Ocean Treaty");
        await fx.CountryVotesAsync(vote, ("FRA", Y), ("BRA", N));
        await fx.PositionAsync(vote, Stance.No, 3);
        await fx.BallotConflictAsync(vote, "BRA", A);

        var page = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}", Ct);

        Assert.Equal(("MA/2024/001", 1), (page.GetProperty("assemblyVoteId").GetString(), page.GetProperty("pendingConflicts").GetInt32()));
        var details = page.GetProperty("details");
        Assert.Equal((79, "2024-01-15", "Ocean Treaty"),
            (details.GetProperty("session").GetInt32(), details.GetProperty("voteDate").GetString(), details.GetProperty("title").GetString()));
        var position = page.GetProperty("position");
        Assert.Equal(("no", 3, "lena"),
            (position.GetProperty("stance").GetString(), position.GetProperty("weight").GetInt32(), position.GetProperty("setBy").GetString()));
    }

    [Fact]
    public async Task A_value_brought_in_by_a_file_names_its_import_line_and_uploader()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var file = await fx.ImportAsync(uploadedBy: "omar", fileName: "q1.csv");
        var vote = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 20), origin: file);
        await fx.CountryVotesAsync(vote, file, ("FRA", Y));

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}", Ct);

        foreach (var origin in new[] { page.GetProperty("details").GetProperty("origin"), CountryVote(page, "FRA").GetProperty("origin") })
        {
            Assert.Equal(("import_row", file.Id, "q1.csv", "omar"),
                (origin.GetProperty("kind").GetString(), origin.GetProperty("importId").GetInt64(),
                 origin.GetProperty("fileName").GetString(), origin.GetProperty("uploadedBy").GetString()));
            Assert.True(origin.GetProperty("lineNo").GetInt32() >= 2);
            Assert.True(origin.GetProperty("importRowId").GetInt64() > 0);
            Assert.Equal(JsonValueKind.String, origin.GetProperty("uploadedAt").ValueKind);
            Assert.Equal(JsonValueKind.Null, origin.GetProperty("decisionId").ValueKind);
        }
        Assert.NotEqual(
            page.GetProperty("details").GetProperty("origin").GetProperty("lineNo").GetInt32(),
            CountryVote(page, "FRA").GetProperty("origin").GetProperty("lineNo").GetInt32());
    }

    [Fact]
    public async Task A_value_approved_by_an_accepted_conflict_names_the_decision()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var ballot = await fx.BallotConflictAsync(vote, "BRA", A);
        var ballotDecision = await fx.DecideAsync(ballot, Decision.Accept, decidedBy: "omar");
        var details = await fx.DetailsConflictAsync(vote, title: "Corrected title");
        var detailsDecision = await fx.DecideAsync(details, Decision.Accept, decidedBy: "dana");

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}", Ct);

        var bra = CountryVote(page, "BRA");
        Assert.Equal("a", bra.GetProperty("ballot").GetString());
        AssertDecisionOrigin(bra.GetProperty("origin"), ballotDecision.Id, ballot.Id, "omar");
        Assert.Equal("Corrected title", page.GetProperty("details").GetProperty("title").GetString());
        AssertDecisionOrigin(page.GetProperty("details").GetProperty("origin"), detailsDecision.Id, details.Id, "dana");
        Assert.Equal(0, page.GetProperty("pendingConflicts").GetInt32());

        static void AssertDecisionOrigin(JsonElement origin, long decisionId, long conflictId, string decidedBy)
        {
            Assert.Equal(("decision", decisionId, conflictId, decidedBy),
                (origin.GetProperty("kind").GetString(), origin.GetProperty("decisionId").GetInt64(),
                 origin.GetProperty("conflictId").GetInt64(), origin.GetProperty("decidedBy").GetString()));
            Assert.Equal(JsonValueKind.String, origin.GetProperty("decidedAt").ValueKind);
            Assert.Equal(JsonValueKind.Null, origin.GetProperty("importId").ValueKind);
        }
    }

    [Fact]
    public async Task Country_votes_list_every_member_on_the_date_and_any_state_with_a_ballot()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        // On 2021-08-01 Iceland (2019-12-31 to 2022-01-01) and New Zealand (until 2021-06-30) are not members.
        var vote = await fx.VoteAsync("MA/2021/040", new DateOnly(2021, 8, 1));
        await fx.CountryVotesAsync(vote, ("FRA", Y), ("NZL", N));

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}", Ct);

        var codes = page.GetProperty("countryVotes").EnumerateArray().Select(c => c.GetProperty("stateCode").GetString()).ToList();
        Assert.Equal(["ARG", "BRA", "CIV", "FRA", "JPN", "MNE", "NZL", "SSD", "TLS", "TUR"], codes);
        var france = CountryVote(page, "FRA");
        Assert.Equal(("France", "y", true),
            (france.GetProperty("stateName").GetString(), france.GetProperty("ballot").GetString(),
             france.GetProperty("memberOnVoteDate").GetBoolean()));
        var brazil = CountryVote(page, "BRA");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, true),
            (brazil.GetProperty("ballot").ValueKind, brazil.GetProperty("origin").ValueKind,
             brazil.GetProperty("memberOnVoteDate").GetBoolean()));
        var newZealand = CountryVote(page, "NZL");
        Assert.Equal(("n", false), (newZealand.GetProperty("ballot").GetString(), newZealand.GetProperty("memberOnVoteDate").GetBoolean()));
    }

    [Fact]
    public async Task A_vote_seen_only_in_a_contradiction_has_no_details()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var file = await fx.ImportAsync();
        long voteId;
        await using (var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString)))
        {
            var vote = new Vote { AssemblyVoteId = "MA/2024/099", FirstSeenImportId = file.Id };
            db.Votes.Add(vote);
            await db.SaveChangesAsync(Ct);
            voteId = vote.Id;
        }

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{voteId}", Ct);

        Assert.Equal(("MA/2024/099", JsonValueKind.Null, JsonValueKind.Null),
            (page.GetProperty("assemblyVoteId").GetString(), page.GetProperty("details").ValueKind, page.GetProperty("position").ValueKind));
        Assert.Empty(page.GetProperty("countryVotes").EnumerateArray());
    }

    [Fact]
    public async Task An_unknown_vote_is_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/votes/987654", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }
}
