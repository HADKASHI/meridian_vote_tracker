using Meridian.Data;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Map ticket 08 rules 9 to 11: file-level warnings.</summary>
public sealed class WarningTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Header = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot,published_at\n";

    [Fact]
    public async Task Rule9_the_same_file_again_is_warned_as_identical_and_changes_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var first = await ImportApi.ImportFixtureAsync(api, "clean.csv");
        var before = await SnapshotAsync(api);

        var again = await ImportApi.ImportFixtureAsync(api, "clean.csv", user: "omar");

        var warning = Assert.Single(again.Warnings, w => w.Kind == ImportWarningKind.IdenticalFile);
        Assert.Equal(first.Id, warning.IdenticalToImportId);
        Assert.Equal($"Identical to import #{first.Id}.", warning.Message);
        Assert.DoesNotContain(first.Warnings, w => w.Kind == ImportWarningKind.IdenticalFile);
        Assert.Equal(new OutcomeCountsDto(0, 5, 0, 0, 0, 0), again.Counts);
        Assert.Empty(again.ConflictsOpened);
        Assert.Equal(before, await SnapshotAsync(api));
    }

    [Fact]
    public async Task Rule10_a_file_older_than_the_newest_already_imported_is_warned()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await ImportApi.ImportAsync(api, "april.csv", Header + "MA/2024/001,79,2024-01-10,Oceans,BRA,Y,2024-04-02T09:00:00Z\n");

        var march = await ImportApi.ImportAsync(api, "march.csv", Header + "MA/2024/002,79,2024-01-12,Seas,BRA,Y,2024-03-01T09:00:00Z\n");
        var may = await ImportApi.ImportAsync(api, "may.csv", Header + "MA/2024/003,79,2024-01-13,Coasts,BRA,Y,2024-05-01T09:00:00Z\n");

        var warning = Assert.Single(march.Warnings, w => w.Kind == ImportWarningKind.OlderPublishedAt);
        Assert.Equal(DateTimeOffset.Parse("2024-04-02T09:00:00Z"), warning.NewestPublishedAt);
        Assert.DoesNotContain(may.Warnings, w => w.Kind == ImportWarningKind.OlderPublishedAt);
        Assert.Equal(new OutcomeCountsDto(1, 0, 0, 0, 0, 0), march.Counts);
    }

    [Fact]
    public async Task Rule11_each_incomplete_vote_is_warned_with_its_missing_members()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using (var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString)))
        {
            var members = await db.MembershipPeriods.Where(p => p.MemberTo == null).Select(p => p.StateCode).Distinct().ToListAsync(Ct);
            var complete = string.Concat(members.Select(code => $"MA/2024/002,79,2024-01-12,Seas,{code.Trim()},Y,\n"));
            var import = await ImportApi.ImportAsync(api, "q1.csv",
                Header + "MA/2024/001,79,2024-01-10,Oceans,BRA,Y,\nMA/2024/001,79,2024-01-10,Oceans,FRA,Yes,\n" + complete);

            var warning = Assert.Single(import.Warnings, w => w.Kind == ImportWarningKind.IncompleteVote);
            Assert.Equal("MA/2024/001", warning.Vote!.AssemblyVoteId);
            Assert.Equal(members.Select(c => c.Trim()).Where(c => c != "BRA").Order(StringComparer.Ordinal), warning.MissingStateCodes);
            Assert.Contains("FRA", warning.MissingStateCodes!);
        }
    }

    /// <summary>Everything an import can change, so a test can show nothing changed.</summary>
    private static async Task<string> SnapshotAsync(ApiFactory api)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var ballots = await db.CurrentCountryVotes.OrderBy(c => c.VoteId).ThenBy(c => c.StateCode)
            .Select(c => $"{c.VoteId}:{c.StateCode}:{c.RevisionId}").ToListAsync(Ct);
        var votes = await db.CurrentVotes.OrderBy(v => v.VoteId).Select(v => $"{v.VoteId}:{v.DetailsRevisionId}").ToListAsync(Ct);
        return string.Join("|", ballots.Concat(votes).Append($"conflicts={await db.Conflicts.CountAsync(Ct)}"));
    }
}
