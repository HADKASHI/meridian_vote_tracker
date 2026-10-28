using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>SNP-01, POS-04: <c>POST /api/snapshots/preview</c> resolves a selection without running a job.</summary>
public sealed class SnapshotPreviewTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> PreviewAsync(ApiFactory api, object selection)
    {
        var response = await api.ClientAs("dana").PostAsJsonAsync("/api/snapshots/preview", selection, Ct);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static string[] AssemblyIds(JsonElement votes) =>
        votes.EnumerateArray().Select(v => v.GetProperty("assemblyVoteId").GetString()!).ToArray();

    [Fact]
    public async Task A_range_is_inclusive_on_approved_dates_and_sessions_lists_and_removed_votes_resolve_as_entered()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var jan1 = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 1), session: 79);
        var mar31 = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 3, 31), session: 79);
        var apr1 = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 4, 1), session: 80);
        // Approved dates count: this vote moved out of the range by an accepted correction.
        var moved = await fx.VoteAsync("MA/2024/004", new DateOnly(2024, 2, 1), session: 80);
        await fx.DecideAsync(await fx.DetailsConflictAsync(moved, voteDate: new DateOnly(2024, 5, 1)), Decision.Accept);
        foreach (var vote in new[] { jan1, mar31, apr1, moved }) await fx.PositionAsync(vote, Stance.Yes, 1);

        Assert.Equal(2, (await PreviewAsync(api, new { from = "2024-01-01", to = "2024-03-31" })).GetProperty("voteCount").GetInt32());
        Assert.Equal(2, (await PreviewAsync(api, new { sessions = new[] { 80 } })).GetProperty("voteCount").GetInt32());
        Assert.Equal(3, (await PreviewAsync(api, new { sessions = new[] { 79, 80 }, excludedVoteIds = new[] { apr1.Id } }))
            .GetProperty("voteCount").GetInt32());
        Assert.Equal(1, (await PreviewAsync(api, new { voteIds = new[] { jan1.Id, mar31.Id }, excludedVoteIds = new[] { mar31.Id } }))
            .GetProperty("voteCount").GetInt32());
    }

    [Fact]
    public async Task A_selection_matching_no_votes_is_refused_with_no_votes_match()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));

        var response = await api.ClientAs("dana").PostAsJsonAsync("/api/snapshots/preview",
            new { from = "2023-01-01", to = "2023-12-31" }, Ct);

        await response.AssertProblemAsync(400, "no-votes-match");
    }

    [Fact]
    public async Task Votes_without_a_position_are_listed_and_still_counted()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var withPosition = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.PositionAsync(withPosition, Stance.No, 3);
        var without = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11));
        await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 1, 12));

        var preview = await PreviewAsync(api, new { from = "2024-01-01", to = "2024-01-31" });

        Assert.Equal(3, preview.GetProperty("voteCount").GetInt32());
        Assert.Equal(["MA/2024/002", "MA/2024/003"], AssemblyIds(preview.GetProperty("noPosition")));
        Assert.Equal(without.Id, preview.GetProperty("noPosition")[0].GetProperty("voteId").GetInt64());
    }

    [Fact]
    public async Task Pending_conflicts_incomplete_votes_and_a_running_import_are_warnings()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        // On 2024-01-10 the members are every seeded state except NZL (left 2021) and ISL is back.
        var complete = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(complete, Members.Select(code => (code, Y)).ToArray());
        await fx.BallotConflictAsync(complete, "FRA", N);
        await fx.BallotConflictAsync(complete, "BRA", A);
        var incomplete = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11));
        await fx.CountryVotesAsync(incomplete, Members.Where(c => c is not ("JPN" or "TUR")).Select(code => (code, N)).ToArray());
        foreach (var vote in new[] { complete, incomplete }) await fx.PositionAsync(vote, Stance.Yes, 2);
        var running = await RunningImportAsync(api.ConnectionString, votesDone: 812, votesTotal: 1204);

        var warnings = (await PreviewAsync(api, new { from = "2024-01-01", to = "2024-01-31" }))
            .GetProperty("warnings").EnumerateArray().ToList();

        Assert.Equal(["import_running", "pending_conflicts", "incomplete_vote"], warnings.Select(w => w.GetProperty("kind").GetString()));
        Assert.Equal((running, 812, 1204), (warnings[0].GetProperty("importId").GetInt64(),
            warnings[0].GetProperty("votesDone").GetInt32(), warnings[0].GetProperty("votesTotal").GetInt32()));
        Assert.Equal(("MA/2024/001", 2), (warnings[1].GetProperty("vote").GetProperty("assemblyVoteId").GetString(),
            warnings[1].GetProperty("pendingConflicts").GetInt32()));
        Assert.Equal("MA/2024/002", warnings[2].GetProperty("vote").GetProperty("assemblyVoteId").GetString());
        Assert.Equal(["JPN", "TUR"], warnings[2].GetProperty("missingStateCodes").EnumerateArray().Select(c => c.GetString()));
        Assert.All(warnings, w => Assert.False(string.IsNullOrWhiteSpace(w.GetProperty("message").GetString())));
    }

    /// <summary>The seeded member states that were members on 2024-01-10.</summary>
    private static readonly string[] Members = ["ARG", "BRA", "CIV", "FRA", "ISL", "JPN", "MNE", "SSD", "TLS", "TUR"];

    /// <summary>An import whose job is applying, as the import handler would leave it part-way.</summary>
    internal static async Task<long> RunningImportAsync(string connectionString, int votesDone, int votesTotal)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
        var import = new Import
        {
            FileName = "running.csv", BlobName = $"imports/{Guid.NewGuid():N}/running.csv",
            Sha256 = new string('a', 64), UploadedBy = "omar",
        };
        db.Imports.Add(import);
        await db.SaveChangesAsync(Ct);
        db.Jobs.Add(new Job
        {
            Kind = JobKind.Import, ImportId = import.Id, State = JobState.Running, Attempts = 1,
            StartedAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow, VotesDone = votesDone, VotesTotal = votesTotal,
        });
        await db.SaveChangesAsync(Ct);
        return import.Id;
    }
}
