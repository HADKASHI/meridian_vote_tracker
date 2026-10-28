using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Fixtures;

/// <summary>The builders leave data exactly as the use cases would; checked through the views and the API.</summary>
public sealed class FixtureBuilderTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Approved_values_have_a_fixture_import_row_as_their_origin()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var fx = Meridian.Fixtures.Fixtures.For(connectionString);

        var vote = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 15), title: "Ocean Treaty");
        await fx.CountryVotesAsync(vote, ("FRA", Y), ("BRA", N));
        await fx.PositionAsync(vote, Stance.Yes, 4);

        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var current = await db.CurrentVotes.SingleAsync(Ct);
        Assert.Equal(("MA/2024/001", "Ocean Treaty", Stance.Yes, (short)4),
            (current.AssemblyVoteId, current.Title, current.Stance!.Value, current.Weight!.Value));
        var france = await db.CurrentCountryVotes.SingleAsync(c => c.StateCode == "FRA", Ct);
        var origin = await db.ImportRows.SingleAsync(r => r.Id == france.OriginImportRowId, Ct);
        Assert.Equal((Outcome.New, france.RevisionId), (origin.Outcome, origin.CountryVoteRevisionId));
        Assert.Equal("dana", (await db.Imports.SingleAsync(i => i.Id == origin.ImportId, Ct)).UploadedBy);
    }

    [Fact]
    public async Task An_accepted_ballot_conflict_moves_the_approved_value_and_keeps_the_old_revision()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var fx = Meridian.Fixtures.Fixtures.For(connectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        var original = (await fx.CountryVotesAsync(vote, ("BRA", Y)))[0];
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);

        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var pending = await db.PendingConflicts.SingleAsync(Ct);
        Assert.Equal((Y, A), (pending.CurrentBallot!.Value, pending.IncomingBallot!.Value));

        var decision = await fx.DecideAsync(conflict, Decision.Accept, comment: "Assembly confirmed");
        await fx.NotAppliedAsync(conflict, Decision.Reject);

        Assert.Empty(await db.PendingConflicts.ToListAsync(Ct));
        var now = await db.CurrentCountryVotes.SingleAsync(Ct);
        Assert.Equal((A, decision.Id), (now.Ballot, now.OriginDecisionId!.Value));
        Assert.Equal(original.Id, decision.ReplacedCountryVoteRevisionId);
        Assert.Equal(2, await db.CountryVoteRevisions.CountAsync(Ct));
    }

    [Fact]
    public async Task A_details_conflict_and_a_published_snapshot_appear_on_the_vote_timeline_in_order()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var fx = Meridian.Fixtures.Fixtures.For(connectionString);
        var vote = await fx.VoteAsync("MA/2024/007", new DateOnly(2024, 3, 1));
        await fx.CountryVotesAsync(vote, ("FRA", Y));
        await fx.PositionAsync(vote, Stance.No, 2);
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2024, 3, 2));
        await fx.DecideAsync(conflict, Decision.Reject, comment: "Date is right");
        var snapshot = await fx.SnapshotAsync("2024-Q1", [vote]);
        var first = await fx.PublishAsync(snapshot, "2024-Q1");
        await fx.PublishAsync(snapshot, "2024-Q1", supersedes: first, reason: "Republished");

        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var kinds = await db.VoteTimeline.Where(e => e.VoteId == vote.Id).OrderBy(e => e.HistoryId)
            .Select(e => e.Kind).ToListAsync(Ct);
        Assert.Equal(
            ["import", "details_revision", "country_vote_revision", "position_revision", "import",
             "conflict_opened", "conflict_decided", "snapshot", "publication", "publication"],
            kinds);
    }

    [Fact]
    public async Task A_calculated_snapshot_stores_lines_and_ranked_results()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var fx = Meridian.Fixtures.Fixtures.For(connectionString);
        var vote = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 20));
        await fx.CountryVotesAsync(vote, ("FRA", Y), ("BRA", Y), ("ARG", N), ("JPN", A), ("NZL", Y));
        await fx.PositionAsync(vote, Stance.Yes, 3);

        var snapshot = await fx.SnapshotAsync("Q1", [vote]);

        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var results = await db.SnapshotResults.Where(r => r.SnapshotId == snapshot.Id)
            .ToDictionaryAsync(r => r.StateCode, r => (r.MatchedWeight, r.CountedWeight, r.Rank), Ct);
        Assert.Equal((3, 3, (int?)1), results["FRA"]);
        Assert.Equal((3, 3, (int?)1), results["BRA"]);
        Assert.Equal((0, 3, (int?)3), results["ARG"]);
        Assert.Equal((0, 0, (int?)null), results["JPN"]);
        var nzl = await db.SnapshotLines.SingleAsync(l => l.SnapshotId == snapshot.Id && l.StateCode == "NZL", Ct);
        Assert.Equal(LineCategory.NonMember, nzl.Category);
        Assert.Equal(LineCategory.NoData,
            (await db.SnapshotLines.SingleAsync(l => l.SnapshotId == snapshot.Id && l.StateCode == "TUR", Ct)).Category);
    }

    [Fact]
    public async Task A_slice_reads_arranged_data_through_the_api()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 1, 25));
        await fx.CountryVotesAsync(vote, ("FRA", Y), ("BRA", N));
        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "rev2.csv");
        await fx.BallotConflictAsync(vote, "FRA", N, openedBy: rev2);
        await fx.BallotConflictAsync(vote, "BRA", A, openedBy: rev2);

        var status = await api.ClientAs("omar").GetFromJsonAsync<JsonElement>("/api/status", Ct);

        Assert.Equal(2, status.GetProperty("pendingConflicts").GetInt32());
        Assert.Equal(2, status.GetProperty("recentlyFinished").GetArrayLength());
    }
}
