using System.Diagnostics;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Domain.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>
/// The 5-minute budget (brief section 9): a synthetic file shaped like the ~1M-row historic
/// one goes through the real API and worker. Run on demand, never in CI.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    public static readonly TimeSpan Budget = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_historic_sized_file_imports_within_five_minutes()
    {
        var file = SyntheticHistoricFile.Generate();
        var member = Path.Combine(Path.GetTempPath(), $"meridian-perf-members-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(member, file.MemberStatesCsv, Ct);
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var db = new MeridianDbContext(MeridianDb.Options(connectionString, member)))
            await db.Database.MigrateAsync(Ct);
        File.Delete(member);
        await using var api = new ApiFactory(connectionString, azurite.ConnectionString);
        // The production time limit: the test worker's 30 s is far too short.
        var settings = new Dictionary<string, string?> { ["Jobs:ImportTimeout"] = "00:30:00" };

        // Warm the host first so the measurement is the import, not the host starting.
        api.ClientAs("dana");
        GC.Collect();
        var baseline = Process.GetCurrentProcess().WorkingSet64;
        using var sampler = new PeakMemory();
        var clock = Stopwatch.StartNew();
        var import = await ImportApi.ImportAsync(api, "assembly_historic_synthetic.csv", file.Csv, workerSettings: settings);
        clock.Stop();
        var peak = sampler.Peak;

        var report = $"Imported {import.Counts.New + import.Counts.Duplicate + import.Counts.Invalid:N0} rows in {clock.Elapsed:mm\\:ss\\.f} " +
                     $"(budget {Budget:mm\\:ss}); peak working set {peak / 1_048_576:N0} MiB, {(peak - baseline) / 1_048_576:N0} MiB above the " +
                     $"{baseline / 1_048_576:N0} MiB before the import.";
        TestContext.Current.SendDiagnosticMessage(report);
        Console.WriteLine(report);
        // The manual workflow points this at the run summary.
        if (Environment.GetEnvironmentVariable("PERFORMANCE_REPORT") is { Length: > 0 } path)
            await File.AppendAllTextAsync(path, $"### Historic-sized import{Environment.NewLine}{Environment.NewLine}{report}{Environment.NewLine}", Ct);

        Assert.Equal(JobState.Done, import.Job.State);
        Assert.Equal(new OutcomeCountsDto(file.Expected.New, 0, file.Expected.Duplicate, file.Expected.Invalid, file.Expected.Contradiction, 0),
            import.Counts);
        Assert.True(clock.Elapsed <= Budget, $"The import took {clock.Elapsed:mm\\:ss}, over the {Budget:mm\\:ss} budget. {report}");
    }

    /// <summary>Samples this process's working set while it lives; the API and worker run in-process.</summary>
    private sealed class PeakMemory : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private long _peak;

        public PeakMemory() => _loop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            using var process = Process.GetCurrentProcess();
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                process.Refresh();
                Interlocked.Exchange(ref _peak, Math.Max(Interlocked.Read(ref _peak), process.WorkingSet64));
            }
        });

        public long Peak => Interlocked.Read(ref _peak);

        public void Dispose()
        {
            _stop.Cancel();
            try { _loop.Wait(); } catch (AggregateException) { }
            _stop.Dispose();
        }
    }
}
