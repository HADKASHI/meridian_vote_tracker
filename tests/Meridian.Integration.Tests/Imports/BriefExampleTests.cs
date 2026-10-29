using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>
/// The brief's section 7.2 example through the API: an original file, then a revised one.
/// Its ids and values are illustrative. Vote 101's title changes in the revised file, so
/// by map ticket 08 rule 5 every row of 101 links to that vote's details conflict.
/// </summary>
public sealed class BriefExampleTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Header = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot\n";

    private const string Original = Header + """
        101,79,2024-01-10,Oceans,BRA,Y
        101,79,2024-01-10,Oceans,IND,Y
        102,79,2024-01-11,Seas,JPN,Y
        102,79,2024-01-11,Seas,JPN,Y
        103,79,2024-01-12,Coasts,FRA,Y
        103,79,2024-01-12,Coasts,FRA,N
        104,79,2024-01-15,Rivers,FRN,X
        104,79,2024-01-15,Rivers,NZL,Y

        """;

    private const string Revised = Header + """
        101,79,2024-01-10,Oceans and Seas,BRA,A
        101,79,2024-01-10,Oceans and Seas,IND,Y
        103,79,2024-01-12,Coasts,FRA,N
        104,79,2024-01-15,Rivers,FRA,X
        105,79,2024-01-16,Lakes,BRA,Y

        """;

    [Fact]
    public async Task The_original_file_then_the_revised_one_give_the_brief_s_outcomes()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await AddIndiaAsync(api);
        var client = api.ClientAs("dana");

        // First upload: the original file.
        var original = await ImportApi.ImportAsync(api, "original.csv", Original);

        Assert.Equal(new OutcomeCountsDto(New: 3, Unchanged: 0, Duplicate: 1, Invalid: 2, Contradiction: 2, Conflict: 0), original.Counts);
        Assert.Equal(
            [(2, Outcome.New), (3, Outcome.New), (4, Outcome.New), (5, Outcome.Duplicate), (6, Outcome.Contradiction),
             (7, Outcome.Contradiction), (8, Outcome.Invalid), (9, Outcome.Invalid)],
            (await RowsAsync(client, original.Id)).Select(r => (r.LineNo, r.Outcome)));
        Assert.Equal(["unknown_country_code", "not_member_on_date"],
            (await RowsAsync(client, original.Id, "invalid")).Select(r => r.ReasonCode));
        Assert.Equal(
            [("101", "BRA", Ballot.Y), ("101", "IND", Ballot.Y), ("102", "JPN", Ballot.Y)],
            await ApprovedBallotsAsync(api));

        // Second upload: the revised file.
        var revised = await ImportApi.ImportAsync(api, "revised.csv", Revised);

        Assert.Equal(new OutcomeCountsDto(New: 3, Unchanged: 0, Duplicate: 0, Invalid: 0, Contradiction: 0, Conflict: 2), revised.Counts);
        var rows = await RowsAsync(client, revised.Id);
        Assert.Equal([Outcome.Conflict, Outcome.Conflict, Outcome.New, Outcome.New, Outcome.New], rows.Select(r => r.Outcome));
        Assert.Equal(2, rows[0].ConflictIds.Count);
        Assert.Single(rows[1].ConflictIds);
        Assert.NotNull(rows[1].CountryVoteRevisionId);

        // BRA keeps Y in 101, its title is unchanged, and 102 is untouched though absent.
        Assert.Equal(
            [("101", "BRA", Ballot.Y), ("101", "IND", Ballot.Y), ("102", "JPN", Ballot.Y), ("103", "FRA", Ballot.N),
             ("104", "FRA", Ballot.X), ("105", "BRA", Ballot.Y)],
            await ApprovedBallotsAsync(api));
        await using (var db = Db(api))
            Assert.Equal("Oceans", (await db.CurrentVotes.SingleAsync(v => v.AssemblyVoteId == "101", Ct)).Title);

        Assert.Equal([(ConflictSubject.Details, null, 2), (ConflictSubject.Ballot, "BRA", 1)],
            revised.ConflictsOpened.Select(c => (c.Subject, c.StateCode, c.Rows)));
        Assert.All(revised.ConflictsOpened, c => Assert.Equal("101", c.Vote.AssemblyVoteId));
        await using (var db = Db(api))
        {
            var conflicts = await db.PendingConflicts.OrderBy(c => c.ConflictId).ToListAsync(Ct);
            Assert.Equal([(Ballot.A, Ballot.Y)], conflicts.Where(c => c.Subject == ConflictSubject.Ballot)
                .Select(c => (c.IncomingBallot!.Value, c.CurrentBallot!.Value)));
            Assert.Equal([("Oceans and Seas", "Oceans")], conflicts.Where(c => c.Subject == ConflictSubject.Details)
                .Select(c => (c.IncomingTitle!, c.CurrentTitle!)));
        }

        // Uploading the revised file again gives nothing new and no new conflicts.
        var again = await ImportApi.ImportAsync(api, "revised.csv", Revised);

        Assert.Equal(0, again.Counts.New);
        Assert.Empty(again.ConflictsOpened);
        Assert.Equal(new OutcomeCountsDto(New: 0, Unchanged: 3, Duplicate: 0, Invalid: 0, Contradiction: 0, Conflict: 2), again.Counts);
        Assert.Equal(["pending_conflict", "pending_conflict"], (await RowsAsync(client, again.Id, "conflict")).Select(r => r.ReasonCode));
    }

    [Fact]
    public async Task A_rejected_value_sent_again_is_previously_rejected_and_opens_nothing()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        const string correction = Header + "101,79,2024-01-10,Oceans,BRA,A\n";
        await ImportApi.ImportAsync(api, "original.csv", Header + "101,79,2024-01-10,Oceans,BRA,Y\n");
        var first = await ImportApi.ImportAsync(api, "correction.csv", correction);
        await using (var db = Db(api))
        await using (var fixtures = new global::Meridian.Fixtures.Fixtures(db))
            await fixtures.DecideAsync(await db.Conflicts.SingleAsync(Ct), Decision.Reject);

        var again = await ImportApi.ImportAsync(api, "correction.csv", correction);

        Assert.Single(first.ConflictsOpened);
        Assert.Empty(again.ConflictsOpened);
        var row = Assert.Single(await RowsAsync(api.ClientAs("dana"), again.Id));
        Assert.Equal((Outcome.Conflict, "previously_rejected"), (row.Outcome, row.ReasonCode));
        Assert.Equal([first.ConflictsOpened[0].ConflictId], row.ConflictIds);
        await using (var db = Db(api))
            Assert.Equal(1, await db.Conflicts.CountAsync(Ct));
    }

    private static async Task<IReadOnlyList<ImportRowDto>> RowsAsync(HttpClient client, long importId, string? outcome = null) =>
        (await ImportApi.GetAsync<PageDto<ImportRowDto>>(client,
            $"/api/imports/{importId}/rows{(outcome is null ? "" : $"?outcome={outcome}")}")).Items;

    private static async Task<List<(string, string, Ballot)>> ApprovedBallotsAsync(ApiFactory api)
    {
        await using var db = Db(api);
        return (await (from c in db.CurrentCountryVotes
                    join v in db.Votes on c.VoteId equals v.Id
                    orderby v.AssemblyVoteId, c.StateCode
                    select new { v.AssemblyVoteId, c.StateCode, c.Ballot })
                .ToListAsync(Ct))
            .Select(x => (x.AssemblyVoteId, x.StateCode.Trim(), x.Ballot)).ToList();
    }

    /// <summary>The committed synthetic member states have no India; the example needs one.</summary>
    private static async Task AddIndiaAsync(ApiFactory api)
    {
        await using var db = Db(api);
        db.MemberStates.Add(new MemberState { Code = "IND", Name = "India" });
        db.MembershipPeriods.Add(new MembershipPeriod { StateCode = "IND", MemberFrom = new DateOnly(1990, 1, 1) });
        await db.SaveChangesAsync(Ct);
    }

    private static MeridianDbContext Db(ApiFactory api) => new(MeridianDb.Options(api.ConnectionString));
}
