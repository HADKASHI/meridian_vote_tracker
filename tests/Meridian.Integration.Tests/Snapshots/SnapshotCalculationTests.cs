using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Jobs;
using Meridian.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Snapshots;

/// <summary>SNP-02 to SNP-04, POS-04: a snapshot is created, calculated as a job, and never changes.</summary>
public sealed class SnapshotCalculationTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object Q1 = new { from = "2024-01-01", to = "2024-03-31" };

    /// <summary>Runs queued jobs with the real snapshot handler, as the worker would.</summary>
    internal static async Task RunJobsAsync(string connectionString)
    {
        using var worker = WorkerHost.Build(connectionString, [new TestJobHandler(JobKind.Snapshot, async (context, ct) =>
        {
            await using var db = new MeridianDbContext(MeridianDb.Options(connectionString));
            await new SnapshotJobHandler(db, NullLogger<SnapshotJobHandler>.Instance).RunAsync(context, ct);
        })]);
        await worker.Processor().RunUntilIdleAsync(Ct);
    }

    internal static async Task<long> CreateAsync(ApiFactory api, object selection, string name = "Q1 2024 index")
    {
        var response = await api.ClientAs("dana").PostAsJsonAsync("/api/snapshots", new { name, note = "First cut", selection }, Ct);
        Assert.Equal(202, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("snapshotId").GetInt64();
    }

    private static Task<JsonElement> GetAsync(ApiFactory api, long snapshotId) =>
        api.ClientAs("omar").GetFromJsonAsync<JsonElement>($"/api/snapshots/{snapshotId}", Ct);

    private static (string, int, int, int, double?, int?)[] Results(JsonElement snapshot) =>
        snapshot.GetProperty("results").EnumerateArray().Select(r => (
            r.GetProperty("stateCode").GetString()!, r.GetProperty("matchedWeight").GetInt32(),
            r.GetProperty("countedWeight").GetInt32(), r.GetProperty("countableVotes").GetInt32(),
            r.GetProperty("score").ValueKind == JsonValueKind.Null ? (double?)null : Math.Round(r.GetProperty("score").GetDouble(), 1),
            r.GetProperty("rank").ValueKind == JsonValueKind.Null ? (int?)null : r.GetProperty("rank").GetInt32()))
            .ToArray();

    /// <summary>Two votes in Q1 with positions; FRA, BRA, ARG, JPN and ISL voted, the other members have no data.</summary>
    private static async Task<(Data.Entities.Vote First, Data.Entities.Vote Second)> ArrangeQ1Async(Meridian.Fixtures.Fixtures fx)
    {
        var first = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10));
        await fx.CountryVotesAsync(first, ("FRA", Y), ("BRA", N), ("ARG", Y), ("JPN", A), ("ISL", Y));
        await fx.PositionAsync(first, Stance.Yes, 3);
        var second = await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 20));
        await fx.CountryVotesAsync(second, ("FRA", N), ("BRA", N), ("ARG", Y), ("JPN", X), ("ISL", N));
        await fx.PositionAsync(second, Stance.No, 1);
        return (first, second);
    }

    [Fact]
    public async Task Create_refuses_a_selection_matching_no_votes_or_holding_a_vote_without_a_position_and_queues_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var without = await fx.VoteAsync("MA/2024/007", new DateOnly(2024, 2, 1));
        var client = api.ClientAs("dana");

        var empty = await client.PostAsJsonAsync("/api/snapshots",
            new { name = "Empty", selection = new { from = "2023-01-01", to = "2023-12-31" } }, Ct);
        await empty.AssertProblemAsync(400, "no-votes-match");

        var blocked = await client.PostAsJsonAsync("/api/snapshots", new { name = "Blocked", selection = Q1 }, Ct);
        var problem = await blocked.AssertProblemAsync(400, "no-position");
        var vote = Assert.Single(problem.GetProperty("votes").EnumerateArray());
        Assert.Equal((without.Id, "MA/2024/007"), (vote.GetProperty("voteId").GetInt64(), vote.GetProperty("assemblyVoteId").GetString()));

        var unnamed = await client.PostAsJsonAsync("/api/snapshots", new { name = "", selection = Q1 }, Ct);
        Assert.True((await unnamed.AssertProblemAsync(400, "validation")).GetProperty("errors").TryGetProperty("name", out _));

        var jobs = await client.GetFromJsonAsync<JsonElement>("/api/jobs", Ct);
        Assert.DoesNotContain(jobs.GetProperty("items").EnumerateArray(), j => j.GetProperty("kind").GetString() == "snapshot");
    }

    [Fact]
    public async Task A_calculated_snapshot_ranks_each_state_by_the_scoring_rules_with_its_basis()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeQ1Async(fx);
        await fx.VoteAsync("MA/2024/099", new DateOnly(2024, 4, 1)); // outside the range, no position: never seen

        var snapshotId = await CreateAsync(api, Q1);
        var queued = await GetAsync(api, snapshotId);
        Assert.Equal("queued", queued.GetProperty("job").GetProperty("state").GetString());

        await RunJobsAsync(api.ConnectionString);
        var snapshot = await GetAsync(api, snapshotId);

        Assert.Equal("done", snapshot.GetProperty("job").GetProperty("state").GetString());
        Assert.Equal(("Q1 2024 index", "First cut", "dana", 2), (snapshot.GetProperty("name").GetString(),
            snapshot.GetProperty("note").GetString(), snapshot.GetProperty("createdBy").GetString(), snapshot.GetProperty("voteCount").GetInt32()));
        Assert.Equal("2024-03-31", snapshot.GetProperty("selection").GetProperty("to").GetString());
        Assert.Equal("alignment-2026.1", snapshot.GetProperty("scoringRulesVersion").GetString());
        // Vote 1 (yes, weight 3), vote 2 (no, weight 1). Worked by hand:
        // ARG Y,Y → 3 of 4 = 75.0; BRA N,N → 1 of 4 = 25.0; FRA Y,N → 4 of 4; ISL Y,N → 4 of 4;
        // JPN A,X → nothing countable; the other members have no data.
        Assert.Equal(
        [
            ("FRA", 4, 4, 2, 100.0, 1), ("ISL", 4, 4, 2, 100.0, 1), ("ARG", 3, 4, 2, 75.0, 3), ("BRA", 1, 4, 2, 25.0, 4),
            ("CIV", 0, 0, 0, null, null), ("JPN", 0, 0, 0, null, null), ("MNE", 0, 0, 0, null, null),
            ("NZL", 0, 0, 0, null, null), ("SSD", 0, 0, 0, null, null), ("TLS", 0, 0, 0, null, null), ("TUR", 0, 0, 0, null, null),
        ], Results(snapshot));
        Assert.Equal("Iceland", snapshot.GetProperty("results")[1].GetProperty("stateName").GetString());
    }

    [Fact]
    public async Task The_job_fails_naming_a_vote_without_a_position_that_joined_the_selection_after_create()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await ArrangeQ1Async(fx);
        var snapshotId = await CreateAsync(api, Q1);
        var late = await fx.ImportAsync(uploadedBy: "omar", fileName: "late.csv");
        await fx.VoteAsync("MA/2024/041", new DateOnly(2024, 3, 15), origin: late);

        await RunJobsAsync(api.ConnectionString);
        var job = (await GetAsync(api, snapshotId)).GetProperty("job");

        Assert.Equal("failed", job.GetProperty("state").GetString());
        Assert.Contains("MA/2024/041", job.GetProperty("lastError").GetString());
        Assert.Contains($"import #{late.Id}", job.GetProperty("lastError").GetString());
    }

    [Fact]
    public async Task A_snapshot_reads_the_same_after_the_data_under_it_changes()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var (first, second) = await ArrangeQ1Async(fx);
        var snapshotId = await CreateAsync(api, Q1);
        await RunJobsAsync(api.ConnectionString);
        var before = (await GetAsync(api, snapshotId)).GetRawText();

        await fx.DecideAsync(await fx.BallotConflictAsync(first, "BRA", Y), Decision.Accept);
        await fx.DecideAsync(await fx.DetailsConflictAsync(second, voteDate: new DateOnly(2024, 5, 1)), Decision.Accept);
        await fx.PositionAsync(first, Stance.No, 5);
        var added = await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 3, 1));
        await fx.CountryVotesAsync(added, ("TUR", Y));
        await fx.PositionAsync(added, Stance.Yes, 2);

        Assert.Equal(before, (await GetAsync(api, snapshotId)).GetRawText());
    }

    [Fact]
    public async Task Warnings_seen_at_calculation_are_stored_on_the_snapshot()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var (first, _) = await ArrangeQ1Async(fx);
        await fx.BallotConflictAsync(first, "FRA", N);
        var snapshotId = await CreateAsync(api, Q1);
        var running = await SnapshotPreviewTests.RunningImportAsync(api.ConnectionString, votesDone: 812, votesTotal: 1204);

        await RunJobsAsync(api.ConnectionString);
        var warnings = (await GetAsync(api, snapshotId)).GetProperty("warnings").EnumerateArray().ToList();

        Assert.Equal(["import_running", "pending_conflicts", "incomplete_vote", "incomplete_vote"],
            warnings.Select(w => w.GetProperty("kind").GetString()));
        Assert.Contains($"Calculated while import #{running} was applying (812 of 1,204 votes)", warnings[0].GetProperty("message").GetString());
        Assert.Equal((1, "MA/2024/001"), (warnings[1].GetProperty("pendingConflicts").GetInt32(),
            warnings[1].GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal(["CIV", "MNE", "SSD", "TLS", "TUR"],
            warnings[2].GetProperty("missingStateCodes").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task An_unknown_snapshot_is_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/snapshots/999999", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }
}
