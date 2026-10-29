using Meridian.Data;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Clean rows through the whole path: classified, written one vote at a time, and reported.</summary>
public sealed class ApplyTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rule14_rows_are_grouped_by_vote_wherever_they_are_and_approved_with_their_row_as_origin()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "clean.csv");

        Assert.Equal(new OutcomeCountsDto(5, 0, 0, 0, 0, 0), import.Counts);
        Assert.Equal(JobState.Done, import.Job.State);
        Assert.Equal((2, 2), (import.Job.VotesDone, import.Job.VotesTotal));

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var votes = await db.CurrentVotes.OrderBy(v => v.AssemblyVoteId).ToListAsync(Ct);
        Assert.Equal(["MA/2024/001", "MA/2024/002"], votes.Select(v => v.AssemblyVoteId));
        Assert.Equal((79, new DateOnly(2024, 1, 12), "Desertification, Including Capacity Building"),
            (votes[1].Session, votes[1].VoteDate, votes[1].Title));

        var ballots = await (from c in db.CurrentCountryVotes
                join v in db.Votes on c.VoteId equals v.Id
                join r in db.ImportRows on c.OriginImportRowId equals r.Id
                orderby v.AssemblyVoteId, c.StateCode
                select new { v.AssemblyVoteId, c.StateCode, c.Ballot, r.LineNo, r.ImportId })
            .ToListAsync(Ct);
        Assert.Equal(
            [("MA/2024/001", "BRA", Ballot.Y, 2), ("MA/2024/001", "FRA", Ballot.N, 3), ("MA/2024/001", "JPN", Ballot.X, 5),
             ("MA/2024/002", "BRA", Ballot.A, 4), ("MA/2024/002", "JPN", Ballot.Y, 6)],
            ballots.Select(b => (b.AssemblyVoteId, b.StateCode, b.Ballot, b.LineNo)));
        Assert.All(ballots, b => Assert.Equal(import.Id, b.ImportId));
    }

    [Fact]
    public async Task Rule13_every_line_has_a_row_record_paged_and_filtered_by_outcome_and_vote()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var import = await ImportApi.ImportFixtureAsync(api, "clean.csv");

        var all = await ImportApi.GetAsync<PageDto<ImportRowDto>>(client, $"/api/imports/{import.Id}/rows?pageSize=2&page=2");
        Assert.Equal(5, all.Total);
        Assert.Equal([4, 5], all.Items.Select(r => r.LineNo));
        var row = all.Items[0];
        Assert.Equal("MA/2024/002,79,2024-01-12,\"Desertification, Including Capacity Building\",BRA,A,Brazil,2024-04-02T09:00:00Z", row.RawText);
        Assert.Equal(Outcome.New, row.Outcome);
        Assert.Equal("new_value", row.ReasonCode);
        Assert.Equal("MA/2024/002", row.Vote!.AssemblyVoteId);
        Assert.Equal("BRA", row.StateCode);
        Assert.NotNull(row.CountryVoteRevisionId);
        Assert.NotNull(row.DetailsRevisionId);

        var vote = row.Vote.VoteId;
        var ofVote = await ImportApi.GetAsync<PageDto<ImportRowDto>>(client, $"/api/imports/{import.Id}/rows?voteId={vote}");
        Assert.Equal([4, 6], ofVote.Items.Select(r => r.LineNo));
        var invalid = await ImportApi.GetAsync<PageDto<ImportRowDto>>(client, $"/api/imports/{import.Id}/rows?outcome=invalid");
        Assert.Equal(0, invalid.Total);
    }

    [Fact]
    public async Task Rule13_counts_per_outcome_and_per_vote_are_queried_from_the_row_records()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var import = await ImportApi.ImportFixtureAsync(api, "clean.csv");

        var perVote = await ImportApi.GetAsync<List<ImportVoteCountsDto>>(client, $"/api/imports/{import.Id}/votes");

        Assert.Equal(["MA/2024/001", "MA/2024/002"], perVote.Select(v => v.Vote.AssemblyVoteId));
        Assert.Equal([3, 2], perVote.Select(v => v.Counts.New));
    }

    [Fact]
    public async Task Rule6_a_conflict_stores_the_incoming_value_and_the_revision_it_was_compared_against()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        const string header = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot\n";
        await ImportApi.ImportAsync(api, "original.csv", header + "MA/2024/001,79,2024-01-10,Oceans,BRA,Y\n");

        var revised = await ImportApi.ImportAsync(api, "revised.csv", header + "MA/2024/001,79,2024-01-10,Oceans and Seas,BRA,A\n");

        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var current = await db.CurrentVotes.SingleAsync(Ct);
        var approvedBallot = await db.CurrentCountryVotes.SingleAsync(Ct);
        var conflicts = await db.Conflicts.Where(c => c.OpenedByImportId == revised.Id).OrderBy(c => c.Id).ToListAsync(Ct);
        Assert.Equal(2, conflicts.Count);
        var details = conflicts.Single(c => c.Subject == ConflictSubject.Details);
        Assert.Equal((79, new DateOnly(2024, 1, 10), "Oceans and Seas", "oceans and seas", current.DetailsRevisionId),
            (details.IncomingSession, details.IncomingVoteDate, details.IncomingTitle, details.IncomingTitleKey, details.ComparedDetailsRevisionId));
        var ballot = conflicts.Single(c => c.Subject == ConflictSubject.Ballot);
        Assert.Equal(("BRA", Ballot.A, approvedBallot.RevisionId),
            (ballot.StateCode!.Trim(), ballot.IncomingBallot, ballot.ComparedCountryVoteRevisionId));
        Assert.Equal(Ballot.Y, approvedBallot.Ballot);
    }

    [Fact]
    public async Task An_unknown_outcome_filter_is_a_validation_problem()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var import = await ImportApi.ImportFixtureAsync(api, "clean.csv");

        var response = await api.ClientAs("dana").GetAsync($"/api/imports/{import.Id}/rows?outcome=rejected", Ct);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("outcome", out _));
    }

    [Fact]
    public async Task The_rows_of_an_unknown_import_are_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");

        await (await client.GetAsync("/api/imports/999999/rows", Ct)).AssertProblemAsync(404, "not-found");
        await (await client.GetAsync("/api/imports/999999/votes", Ct)).AssertProblemAsync(404, "not-found");
    }
}
