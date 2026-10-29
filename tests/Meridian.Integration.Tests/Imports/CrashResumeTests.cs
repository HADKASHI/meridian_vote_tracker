using Meridian.Data;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Meridian.Integration.Tests.Imports;

/// <summary>
/// A retried import resumes from the votes it committed (ADR 0003), through the real
/// BackgroundService. The crash is a trigger in the test's own database that fails, or
/// stalls, the vote holding one line on its first pass only; a sequence counts passes,
/// because it survives the rollback. The stall is short because stopping a worker inside a
/// COPY does not cancel the statement on the server: the vote's lock is held until the stall
/// ends, and the next worker waits for it.
/// </summary>
public sealed class CrashResumeTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Header = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot\n";

    /// <summary>Four votes; line 6 is in the third. Vote 001 is approved first, so the file also opens a conflict.</summary>
    private const string File = Header + """
        MA/2024/001,79,2024-01-10,Oceans,BRA,A
        MA/2024/001,79,2024-01-10,Oceans,FRA,N
        MA/2024/002,79,2024-01-11,Seas,BRA,Y
        MA/2024/003,79,2024-01-12,Coasts,BRA,Y
        MA/2024/003,79,2024-01-12,Coasts,JPN,Yes
        MA/2024/004,79,2024-01-13,Rivers,BRA,N

        """;

    [Fact]
    public async Task A_retried_import_resumes_and_ends_with_the_same_report_as_an_uninterrupted_one()
    {
        await using var uninterrupted = await ApiFactory.CreateAsync(postgres, azurite);
        await ArrangeAsync(uninterrupted);
        var expected = await ImportApi.ImportAsync(uninterrupted, "q1.csv", File);

        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await ArrangeAsync(api);
        await CrashOnceAtLineAsync(api, 6, "RAISE EXCEPTION 'worker crashed'");
        var created = await ImportApi.UploadOkAsync(api.ClientAs("dana"), "q1.csv", File);

        using (var worker = ImportApi.Worker(api))
        {
            await worker.StartAsync(Ct);
            var job = await WaitForAsync(api, created.ImportId, j => j.State == JobState.Done);
            await worker.StopAsync(Ct);
            Assert.Equal(2, job.Attempts);
        }

        var import = await ImportApi.GetAsync<ImportDto>(api.ClientAs("dana"), $"/api/imports/{created.ImportId}");
        Assert.Equal(expected.Counts, import.Counts);
        Assert.Equal((4, 4), (import.Job.VotesDone, import.Job.VotesTotal));
        Assert.Equal(await ReportAsync(uninterrupted, expected.Id), await ReportAsync(api, import.Id));
    }

    [Fact]
    public async Task A_worker_stopped_mid_import_hands_it_back_and_a_new_worker_finishes_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await ArrangeAsync(api);
        await CrashOnceAtLineAsync(api, 6, "PERFORM pg_sleep(5)");
        var created = await ImportApi.UploadOkAsync(api.ClientAs("dana"), "q1.csv", File);

        using (var first = ImportApi.Worker(api))
        {
            await first.StartAsync(Ct);
            await WaitForAsync(api, created.ImportId, j => j.VotesDone == 2);
            await WaitForCrashPointAsync(api);
            await first.StopAsync(Ct);
        }
        var handedBack = await JobAsync(api, created.ImportId);
        Assert.Equal((JobState.Queued, 2), (handedBack.State, handedBack.VotesDone));
        Assert.Equal(3, await RowCountAsync(api, created.ImportId));

        using (var second = ImportApi.Worker(api))
        {
            await second.StartAsync(Ct);
            await WaitForAsync(api, created.ImportId, j => j.State == JobState.Done);
            await second.StopAsync(Ct);
        }
        Assert.Equal(6, await RowCountAsync(api, created.ImportId));
        var import = await ImportApi.GetAsync<ImportDto>(api.ClientAs("dana"), $"/api/imports/{created.ImportId}");
        Assert.Equal(new OutcomeCountsDto(4, 0, 0, 1, 0, 1), import.Counts);
    }

    [Fact]
    public async Task A_failed_import_keeps_how_many_votes_it_committed()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await ArrangeAsync(api);
        await CrashOnceAtLineAsync(api, 6, "RAISE EXCEPTION 'worker crashed'");
        var created = await ImportApi.UploadOkAsync(api.ClientAs("dana"), "q1.csv", File);

        using (var worker = ImportApi.Worker(api, new Dictionary<string, string?> { ["Jobs:MaxAttempts"] = "1" }))
            await worker.Processor().RunUntilIdleAsync(Ct);

        var job = await JobAsync(api, created.ImportId);
        Assert.Equal((JobState.Failed, 2, 4), (job.State, job.VotesDone, job.VotesTotal));
        Assert.Equal("Unexpected PostgresException; find it in the logs by the job's trace id.", job.LastError);
    }

    /// <summary>Approves vote 001 with BRA Y, so the file's BRA A is a conflict.</summary>
    private static Task ArrangeAsync(ApiFactory api) =>
        ImportApi.ImportAsync(api, "approved.csv", Header + "MA/2024/001,79,2024-01-10,Oceans,BRA,Y\n");

    private static async Task CrashOnceAtLineAsync(ApiFactory api, int lineNo, string action)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"""
            CREATE SEQUENCE crash_pass;
            CREATE FUNCTION crash_once() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW.line_no = {lineNo} AND nextval('crash_pass') = 1 THEN {action}; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER crash_once BEFORE INSERT ON import_row FOR EACH ROW EXECUTE FUNCTION crash_once();
            """, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<Data.Entities.Job> JobAsync(ApiFactory api, long importId)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        return await db.Jobs.AsNoTracking().SingleAsync(j => j.ImportId == importId, Ct);
    }

    private static async Task<Data.Entities.Job> WaitForAsync(ApiFactory api, long importId, Func<Data.Entities.Job, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var job = await JobAsync(api, importId);
            if (condition(job)) return job;
            Assert.True(DateTime.UtcNow < deadline, $"Job {job.Id} is {job.State} with {job.VotesDone} votes done after 30 s.");
            await Task.Delay(50, Ct);
        }
    }

    /// <summary>
    /// Waits until the trigger has taken its one pass. Two votes done does not say so: stopping the
    /// worker before it reaches the line would leave the stall for the new worker.
    /// </summary>
    private static async Task WaitForCrashPointAsync(ApiFactory api)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT is_called FROM crash_pass", connection);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!(bool)(await command.ExecuteScalarAsync(Ct))!)
        {
            Assert.True(DateTime.UtcNow < deadline, "The worker never reached the line the trigger watches.");
            await Task.Delay(50, Ct);
        }
    }

    private static async Task<int> RowCountAsync(ApiFactory api, long importId)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        return await db.ImportRows.CountAsync(r => r.ImportId == importId, Ct);
    }

    /// <summary>The report without ids: every row's verdict and links, the approved values, conflicts and warnings.</summary>
    private static async Task<string> ReportAsync(ApiFactory api, long importId)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var rows = await db.ImportRows.Where(r => r.ImportId == importId).OrderBy(r => r.LineNo)
            .Select(r => $"{r.LineNo}:{r.Outcome}:{r.ReasonCode}:{r.CountryVoteRevisionId != null}:{r.DetailsRevisionId != null}:"
                + db.ImportRowConflicts.Count(l => l.ImportRowId == r.Id))
            .ToListAsync(Ct);
        var ballots = await (from c in db.CurrentCountryVotes join v in db.Votes on c.VoteId equals v.Id
                orderby v.AssemblyVoteId, c.StateCode select $"{v.AssemblyVoteId}:{c.StateCode}:{c.Ballot}")
            .ToListAsync(Ct);
        var conflicts = await db.Conflicts.Where(c => c.OpenedByImportId == importId).OrderBy(c => c.StateCode)
            .Select(c => $"{c.Subject}:{c.StateCode}:{c.IncomingBallot}").ToListAsync(Ct);
        var warnings = await (from w in db.ImportWarnings where w.ImportId == importId
                join v in db.Votes on w.VoteId equals v.Id into vs from v in vs.DefaultIfEmpty()
                orderby w.Kind, v.AssemblyVoteId select $"{w.Kind}:{v.AssemblyVoteId}:{w.Detail}")
            .ToListAsync(Ct);
        return string.Join("\n", rows.Concat(ballots).Concat(conflicts).Concat(warnings));
    }
}
