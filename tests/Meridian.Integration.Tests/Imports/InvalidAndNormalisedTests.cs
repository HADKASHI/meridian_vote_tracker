using Meridian.Data;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Map ticket 08 rules 2, 3 and 12 through the API.</summary>
public sealed class InvalidAndNormalisedTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_invalid_row_gets_its_reason_and_nothing_is_approved_for_it()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "invalid-rows.csv");

        Assert.Equal(new OutcomeCountsDto(1, 0, 0, 8, 0, 0), import.Counts);
        var rows = await ImportApi.GetAsync<PageDto<ImportRowDto>>(api.ClientAs("dana"), $"/api/imports/{import.Id}/rows");
        Assert.Equal(
            [(2, "new_value"), (3, "field_count"), (4, "missing_field"), (5, "unknown_country_code"), (6, "invalid_ballot"),
             (7, "empty_ballot"), (8, "invalid_date"), (9, "invalid_session"), (10, "not_member_on_date")],
            rows.Items.Select(r => (r.LineNo, r.ReasonCode!)));
        Assert.Null(rows.Items[1].Vote);
        Assert.Equal("MA/2024/001", rows.Items[3].Vote!.AssemblyVoteId);
        Assert.Equal("FRN", rows.Items[3].StateCode);
        Assert.All(rows.Items.Skip(1), r => Assert.Null(r.CountryVoteRevisionId));

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(["BRA"], await db.CurrentCountryVotes.Select(c => c.StateCode).ToListAsync(Ct));
        Assert.Equal(["MA/2024/001"], await db.CurrentVotes.Select(v => v.AssemblyVoteId).ToListAsync(Ct));
        Assert.Equal(3, await db.Votes.CountAsync(Ct));
    }

    [Fact]
    public async Task Normalised_values_are_stored_and_each_normalisation_is_listed_on_its_row()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "normalisations.csv");

        Assert.Equal(new OutcomeCountsDto(2, 0, 0, 0, 0, 0), import.Counts);
        var rows = await ImportApi.GetAsync<PageDto<ImportRowDto>>(api.ClientAs("dana"), $"/api/imports/{import.Id}/rows");
        Assert.Equal(
            ["country_code: trimmed", "country_code: upper-cased", "ballot: upper-cased", "assembly_vote_id: trimmed",
             "vote_date: 10/01/2024 read as 2024-01-10", "resolution_title: invisible characters removed"],
            rows.Items[0].Normalisations);
        Assert.Equal(["vote_date: 10/01/2024 read as 2024-01-10"], rows.Items[1].Normalisations);

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var vote = await db.CurrentVotes.SingleAsync(Ct);
        Assert.Equal(("MA/2024/001", new DateOnly(2024, 1, 10), "Oceans and Seas"), (vote.AssemblyVoteId, vote.VoteDate, vote.Title));
        Assert.Equal([("BRA", Ballot.Y), ("FRA", Ballot.N)],
            (await db.CurrentCountryVotes.OrderBy(c => c.StateCode).ToListAsync(Ct)).Select(c => (c.StateCode, c.Ballot)));
    }
}
