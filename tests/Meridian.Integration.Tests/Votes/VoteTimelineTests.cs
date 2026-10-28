using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Votes;

/// <summary>HIS-03, HIS-04, user stories 68 to 70: a vote's whole story, ordered by history id and paged.</summary>
public sealed class VoteTimelineTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Reads every page of the timeline, oldest page last, and checks the paging links on the way.</summary>
    private static async Task<List<JsonElement>> ReadAllAsync(HttpClient client, long voteId, int pageSize)
    {
        var events = new List<JsonElement>();
        long? before = null;
        do
        {
            var page = await client.GetFromJsonAsync<JsonElement>(
                $"/api/votes/{voteId}/timeline?pageSize={pageSize}{(before is null ? "" : $"&before={before}")}", Ct);
            var items = page.GetProperty("items").EnumerateArray().ToList();
            Assert.InRange(items.Count, 1, pageSize);
            events.AddRange(items);
            before = page.GetProperty("nextBefore").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextBefore").GetInt64();
            if (before is not null) Assert.Equal(items[^1].GetProperty("historyId").GetInt64(), before);
        } while (before is not null);
        return events;
    }

    [Fact]
    public async Task The_timeline_shows_every_event_kind_newest_first_across_pages()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var q1 = await fx.ImportAsync(uploadedBy: "dana", fileName: "q1.csv");
        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 15), session: 79, title: "Ocean Treaty", origin: q1);
        await fx.CountryVotesAsync(vote, q1, ("FRA", Y), ("BRA", Y));
        await fx.PositionAsync(vote, Stance.Yes, 4);
        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "rev2.csv");
        var ballot = await fx.BallotConflictAsync(vote, "BRA", A, openedBy: rev2);
        await fx.DecideAsync(ballot, Decision.Accept, decidedBy: "dana", comment: "Assembly confirmed");
        await fx.NotAppliedAsync(ballot, Decision.Reject, attemptedBy: "omar");
        var snapshot = await fx.SnapshotAsync("2024-Q1 index", [vote], createdBy: "omar");
        var first = await fx.PublishAsync(snapshot, "2024-Q1");
        await fx.PublishAsync(snapshot, "2024-Q1", supersedes: first, reason: "Republished after review");

        var events = await ReadAllAsync(api.ClientAs("dana"), vote.Id, pageSize: 4);

        Assert.Equal(
            ["publication", "publication", "snapshot", "decision_not_applied", "country_vote_revision", "conflict_decided",
             "conflict_opened", "import", "position_revision", "country_vote_revision", "country_vote_revision",
             "details_revision", "import"],
            events.Select(e => e.GetProperty("kind").GetString()));
        var ids = events.Select(e => e.GetProperty("historyId").GetInt64()).ToList();
        Assert.Equal(ids.OrderDescending(), ids);

        var byKind = events.ToLookup(e => e.GetProperty("kind").GetString());
        var imports = byKind["import"].ToList();
        Assert.Equal(("rev2.csv", rev2.Id, "omar", 1),
            (imports[0].GetProperty("fileName").GetString(), imports[0].GetProperty("importId").GetInt64(),
             imports[0].GetProperty("actor").GetString(), imports[0].GetProperty("counts").GetProperty("conflict").GetInt32()));
        Assert.Equal(("q1.csv", 3, 0),
            (imports[1].GetProperty("fileName").GetString(), imports[1].GetProperty("counts").GetProperty("new").GetInt32(),
             imports[1].GetProperty("counts").GetProperty("conflict").GetInt32()));

        var details = byKind["details_revision"].Single();
        Assert.Equal((79, "2024-01-15", "Ocean Treaty"),
            (details.GetProperty("to").GetProperty("session").GetInt32(), details.GetProperty("to").GetProperty("voteDate").GetString(),
             details.GetProperty("to").GetProperty("title").GetString()));
        Assert.Equal(("import_row", "q1.csv", "dana"),
            (details.GetProperty("origin").GetProperty("kind").GetString(), details.GetProperty("origin").GetProperty("fileName").GetString(),
             details.GetProperty("actor").GetString()));

        var accepted = byKind["country_vote_revision"].First();
        Assert.Equal(("BRA", "a", "decision", ballot.Id),
            (accepted.GetProperty("stateCode").GetString(), accepted.GetProperty("to").GetProperty("ballot").GetString(),
             accepted.GetProperty("origin").GetProperty("kind").GetString(), accepted.GetProperty("origin").GetProperty("conflictId").GetInt64()));

        var position = byKind["position_revision"].Single();
        Assert.Equal(("yes", 4, "lena"),
            (position.GetProperty("to").GetProperty("stance").GetString(), position.GetProperty("to").GetProperty("weight").GetInt32(),
             position.GetProperty("actor").GetString()));

        var opened = byKind["conflict_opened"].Single();
        Assert.Equal((ballot.Id, "ballot", "BRA", "a", rev2.Id, "rev2.csv"),
            (opened.GetProperty("conflictId").GetInt64(), opened.GetProperty("subject").GetString(), opened.GetProperty("stateCode").GetString(),
             opened.GetProperty("to").GetProperty("ballot").GetString(), opened.GetProperty("importId").GetInt64(),
             opened.GetProperty("fileName").GetString()));

        var decided = byKind["conflict_decided"].Single();
        Assert.Equal((ballot.Id, "accept", "Assembly confirmed", "dana", "BRA"),
            (decided.GetProperty("conflictId").GetInt64(), decided.GetProperty("decision").GetString(),
             decided.GetProperty("comment").GetString(), decided.GetProperty("actor").GetString(), decided.GetProperty("stateCode").GetString()));

        var lost = byKind["decision_not_applied"].Single();
        Assert.Equal((ballot.Id, "reject", "omar"),
            (lost.GetProperty("conflictId").GetInt64(), lost.GetProperty("decision").GetString(), lost.GetProperty("actor").GetString()));

        var snap = byKind["snapshot"].Single();
        Assert.Equal((snapshot.Id, "2024-Q1 index", "omar"),
            (snap.GetProperty("snapshotId").GetInt64(), snap.GetProperty("snapshotName").GetString(), snap.GetProperty("actor").GetString()));

        var publications = byKind["publication"].ToList();
        Assert.Equal(("2024-Q1", "Republished after review", snapshot.Id, "2024-Q1 index"),
            (publications[0].GetProperty("quarter").GetString(), publications[0].GetProperty("reason").GetString(),
             publications[0].GetProperty("snapshotId").GetInt64(), publications[0].GetProperty("snapshotName").GetString()));
        Assert.Equal(JsonValueKind.Null, publications[1].GetProperty("reason").ValueKind);
        Assert.All(events, e => Assert.Equal(JsonValueKind.String, e.GetProperty("at").ValueKind));
    }

    [Fact]
    public async Task A_details_conflict_shows_its_incoming_details()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 1), session: 79, title: "Outer Space Accord");
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2024, 2, 2));
        await fx.DecideAsync(conflict, Decision.Reject, decidedBy: "omar", comment: "Date is right");

        var events = await ReadAllAsync(api.ClientAs("dana"), vote.Id, pageSize: 50);

        var opened = events.Single(e => e.GetProperty("kind").GetString() == "conflict_opened");
        Assert.Equal(("details", "2024-02-02", 79, "Outer Space Accord"),
            (opened.GetProperty("subject").GetString(), opened.GetProperty("to").GetProperty("voteDate").GetString(),
             opened.GetProperty("to").GetProperty("session").GetInt32(), opened.GetProperty("to").GetProperty("title").GetString()));
        var decided = events.Single(e => e.GetProperty("kind").GetString() == "conflict_decided");
        Assert.Equal(("details", "reject", "Date is right"),
            (decided.GetProperty("subject").GetString(), decided.GetProperty("decision").GetString(), decided.GetProperty("comment").GetString()));
        Assert.Single(events, e => e.GetProperty("kind").GetString() == "details_revision");
    }

    [Fact]
    public async Task A_page_size_is_clamped_and_the_last_page_has_no_next()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 2, 10));
        await fx.CountryVotesAsync(vote, ("FRA", Y));

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}/timeline?pageSize=0", Ct);
        Assert.Single(page.GetProperty("items").EnumerateArray());
        var all = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/votes/{vote.Id}/timeline", Ct);
        Assert.Equal(3, all.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, all.GetProperty("nextBefore").ValueKind);
    }

    [Fact]
    public async Task An_unknown_vote_has_no_timeline()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/votes/987654/timeline", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }
}
