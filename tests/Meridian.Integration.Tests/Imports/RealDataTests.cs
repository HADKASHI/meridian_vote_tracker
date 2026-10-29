using Meridian.Data;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>
/// The real assignment files give the counts the data audit predicted (map ticket 08). Run
/// on demand, never in CI: the files are client data and are never committed, so these
/// tests skip themselves when <c>candidate_data/</c> is missing.
/// </summary>
[Trait("Category", "RealData")]
public sealed class RealDataTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_q1_files_give_the_audited_counts()
    {
        var data = DataDirectory();
        await using var api = await RealApiAsync(data);

        var original = await ImportFileAsync(api, data, "assembly_export_2024_q1.csv");
        Assert.Equal(new OutcomeCountsDto(New: 5_737, Unchanged: 0, Duplicate: 3, Invalid: 5, Contradiction: 151, Conflict: 0),
            original.Counts);

        var rev2 = await ImportFileAsync(api, data, "assembly_export_2024_q1_rev2.csv");
        Assert.Equal(new OutcomeCountsDto(New: 372, Unchanged: 5_284, Duplicate: 0, Invalid: 0, Contradiction: 0, Conflict: 304),
            rev2.Counts);
        Assert.Equal((6, 2), (rev2.ConflictsOpened.Count(c => c.Subject == ConflictSubject.Ballot),
            rev2.ConflictsOpened.Count(c => c.Subject == ConflictSubject.Details)));

        var again = await ImportFileAsync(api, data, "assembly_export_2024_q1_rev2.csv");
        Assert.Empty(again.ConflictsOpened);
        Assert.Equal(new OutcomeCountsDto(New: 0, Unchanged: 5_656, Duplicate: 0, Invalid: 0, Contradiction: 0, Conflict: 304),
            again.Counts);
        Assert.Contains(again.Warnings, w => w.Kind == ImportWarningKind.IdenticalFile && w.IdenticalToImportId == rev2.Id);
    }

    [Fact]
    public async Task The_historic_file_gives_the_audited_counts()
    {
        var data = DataDirectory();
        await using var api = await RealApiAsync(data);

        var historic = await ImportFileAsync(api, data, "assembly_historic.csv");

        Assert.Equal(JobState.Done, historic.Job.State);
        Assert.Equal((962_109, 30, 32), (historic.Counts.New, historic.Counts.Duplicate, historic.Counts.Invalid));
    }

    /// <summary>The production time limit: the test worker's 30 s is far too short for the historic file.</summary>
    private static readonly Dictionary<string, string?> ProductionTimeout = new() { ["Jobs:ImportTimeout"] = "00:30:00" };

    private static Task<ImportDto> ImportFileAsync(ApiFactory api, string data, string fileName) =>
        ImportApi.ImportAsync(api, fileName, File.ReadAllBytes(Path.Combine(data, fileName)), workerSettings: ProductionTimeout);

    /// <summary>An API on a database seeded with the real member states, not the synthetic ones.</summary>
    private async Task<ApiFactory> RealApiAsync(string data)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using (var db = new MeridianDbContext(MeridianDb.Options(connectionString, Path.Combine(data, "member_states.csv"))))
            await db.Database.MigrateAsync(Ct);
        return new ApiFactory(connectionString, azurite.ConnectionString);
    }

    /// <summary><c>candidate_data/</c> at the repository root, or skips the test.</summary>
    private static string DataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Meridian.slnx")))
            directory = directory.Parent;
        var data = directory is null ? null : Path.Combine(directory.FullName, "candidate_data");
        string[] files = ["member_states.csv", "assembly_export_2024_q1.csv", "assembly_export_2024_q1_rev2.csv", "assembly_historic.csv"];
        Assert.SkipUnless(data is not null && files.All(f => File.Exists(Path.Combine(data, f))),
            "candidate_data/ with the assignment files is missing; real-data tests run only where the files are present.");
        return data!;
    }
}
