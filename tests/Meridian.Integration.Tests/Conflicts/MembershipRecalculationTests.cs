using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Conflicts;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Meridian.Domain.Ballot;

namespace Meridian.Integration.Tests.Conflicts;

/// <summary>
/// Every vote-details decision recalculates membership for its vote (map ticket 08 rule 8;
/// stories 60–62). Synthetic members: NZL until 2021-06-30, SSD from 2011-07-14.
/// </summary>
public sealed class MembershipRecalculationTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("NZL", "2021-07-01", "2021-06-30")] // the last day of membership counts
    [InlineData("SSD", "2011-07-13", "2011-07-14")] // the first day of membership counts
    public async Task A_date_correction_approves_ballots_invalid_only_for_membership_with_the_decision_as_origin(
        string state, string approvedDate, string correctedDate)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var import = await fx.ImportAsync(fileName: "q1.csv");
        var vote = await fx.VoteAsync("MA/2024/031", DateOnly.Parse(approvedDate), origin: import);
        await fx.CountryVotesAsync(vote, import, ("FRA", Y));
        var notMember = await InvalidRowAsync(api, import, vote, state, "N", MembershipRecalculation.NotMemberReasonCode);
        await InvalidRowAsync(api, import, vote, "XXX", "Y", "unknown_country_code");
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: DateOnly.Parse(correctedDate));

        var decision = await DecideAsync(api, conflict.Id, "accept");

        var membership = decision.GetProperty("membership");
        Assert.Equal([state], Strings(membership.GetProperty("approvedStateCodes")));
        Assert.Empty(membership.GetProperty("noLongerCountedStateCodes").EnumerateArray());
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var approved = await db.CurrentCountryVotes.SingleAsync(c => c.VoteId == vote.Id && c.StateCode == state, Ct);
        Assert.Equal((N, decision.GetProperty("id").GetInt64()), (approved.Ballot, approved.OriginDecisionId));
        var row = await db.ImportRows.SingleAsync(r => r.Id == notMember.Id, Ct);
        Assert.Equal((Outcome.Invalid, (long?)null), (row.Outcome, row.CountryVoteRevisionId));
    }

    [Fact]
    public async Task Approved_ballots_now_outside_membership_stay_stored_but_stop_counting()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2021/014", new DateOnly(2021, 6, 30));
        var nzl = (await fx.CountryVotesAsync(vote, ("NZL", Y), ("FRA", N)))[0];
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 7, 1));

        var decision = await DecideAsync(api, conflict.Id, "accept");

        var membership = decision.GetProperty("membership");
        Assert.Equal(["NZL"], Strings(membership.GetProperty("noLongerCountedStateCodes")));
        Assert.Empty(membership.GetProperty("approvedStateCodes").EnumerateArray());
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(nzl.Id, (await db.CurrentCountryVotes.SingleAsync(c => c.VoteId == vote.Id && c.StateCode == "NZL", Ct)).RevisionId);

        var pane = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/conflicts/{conflict.Id}", Ct);
        Assert.Equal(["NZL"], Strings(pane.GetProperty("decision").GetProperty("membership").GetProperty("noLongerCountedStateCodes")));
    }

    [Fact]
    public async Task A_state_whose_current_country_vote_never_counted_is_not_reported_as_no_longer_counted()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2021/015", new DateOnly(2021, 6, 30));
        await fx.CountryVotesAsync(vote, ("NZL", Y));
        await fx.DecideAsync(await fx.BallotConflictAsync(vote, "NZL", A), Decision.Accept);
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 7, 1));

        var decision = await DecideAsync(api, conflict.Id, "accept");

        Assert.Empty(decision.GetProperty("membership").GetProperty("noLongerCountedStateCodes").EnumerateArray());
    }

    [Fact]
    public async Task The_earliest_row_is_approved_and_each_later_different_ballot_opens_a_conflict()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var first = await fx.ImportAsync(uploadedBy: "dana", fileName: "q1.csv");
        var second = await fx.ImportAsync(uploadedBy: "omar", fileName: "q1-rev2.csv");
        var third = await fx.ImportAsync(uploadedBy: "dana", fileName: "q1-rev3.csv");
        var vote = await fx.VoteAsync("MA/2021/020", new DateOnly(2021, 7, 2), origin: first);
        await InvalidRowAsync(api, first, vote, "NZL", "Y", MembershipRecalculation.NotMemberReasonCode);
        var different = await InvalidRowAsync(api, second, vote, "NZL", "A", MembershipRecalculation.NotMemberReasonCode);
        await InvalidRowAsync(api, third, vote, "NZL", "Y", MembershipRecalculation.NotMemberReasonCode);
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 6, 2));

        var decision = await DecideAsync(api, conflict.Id, "accept");

        var opened = Assert.Single(decision.GetProperty("membership").GetProperty("openedConflictIds").EnumerateArray()).GetInt64();
        var inbox = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/conflicts", Ct);
        var item = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal((opened, "NZL", "y", "a", second.Id), (item.GetProperty("id").GetInt64(), item.GetProperty("stateCode").GetString(),
            item.GetProperty("current").GetProperty("ballot").GetString(), item.GetProperty("incoming").GetProperty("ballot").GetString(),
            item.GetProperty("importId").GetInt64()));
        var pane = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>($"/api/conflicts/{opened}", Ct);
        Assert.Equal(different.Id, pane.GetProperty("incoming").GetProperty("origin").GetProperty("importRowId").GetInt64());
        Assert.Equal("decision", pane.GetProperty("current").GetProperty("origin").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_state_that_already_has_an_approved_ballot_is_left_as_it_is()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var import = await fx.ImportAsync();
        var vote = await fx.VoteAsync("MA/2021/021", new DateOnly(2021, 7, 2), origin: import);
        await InvalidRowAsync(api, import, vote, "NZL", "N", MembershipRecalculation.NotMemberReasonCode);
        var earlier = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 6, 2));
        await DecideAsync(api, earlier.Id, "accept");
        var later = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 6, 3));

        var decision = await DecideAsync(api, later.Id, "accept");

        Assert.Empty(decision.GetProperty("membership").GetProperty("approvedStateCodes").EnumerateArray());
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.Equal(1, await db.CountryVoteRevisions.CountAsync(r => r.VoteId == vote.Id && r.StateCode == "NZL", Ct));
    }

    [Fact]
    public async Task A_rejected_details_decision_recalculates_too_and_with_the_date_unchanged_nothing_changes()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var import = await fx.ImportAsync();
        var vote = await fx.VoteAsync("MA/2021/022", new DateOnly(2021, 7, 2), origin: import);
        await InvalidRowAsync(api, import, vote, "NZL", "N", MembershipRecalculation.NotMemberReasonCode);
        var conflict = await fx.DetailsConflictAsync(vote, voteDate: new DateOnly(2021, 6, 2));

        var decision = await DecideAsync(api, conflict.Id, "reject");

        var membership = decision.GetProperty("membership");
        Assert.Empty(membership.GetProperty("approvedStateCodes").EnumerateArray());
        Assert.Empty(membership.GetProperty("noLongerCountedStateCodes").EnumerateArray());
        Assert.Empty(membership.GetProperty("openedConflictIds").EnumerateArray());
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        Assert.False(await db.CountryVoteRevisions.AnyAsync(r => r.VoteId == vote.Id, Ct));
    }

    [Fact]
    public async Task A_ballot_decision_has_no_membership_recalculation()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var vote = await fx.VoteAsync("MA/2024/101", new DateOnly(2024, 2, 1));
        await fx.CountryVotesAsync(vote, ("BRA", Y));
        var conflict = await fx.BallotConflictAsync(vote, "BRA", A);

        var decision = await DecideAsync(api, conflict.Id, "accept");

        Assert.Equal(JsonValueKind.Null, decision.GetProperty("membership").ValueKind);
    }

    private static async Task<JsonElement> DecideAsync(ApiFactory api, long conflictId, string decision)
    {
        var response = await api.ClientAs("dana").PostAsJsonAsync($"/api/conflicts/{conflictId}/decision", new { decision }, Ct);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>
    /// A row record the importer judged invalid, as slice 1 writes it. Arranged here
    /// because the fixture builders only write rows that produced approved values or conflicts.
    /// </summary>
    private static async Task<ImportRow> InvalidRowAsync(ApiFactory api, Import import, Vote vote, string state, string ballot,
        string reasonCode)
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(api.ConnectionString));
        var lineNo = await db.ImportRows.Where(r => r.ImportId == import.Id).MaxAsync(r => (int?)r.LineNo, Ct) ?? 1;
        var row = new ImportRow
        {
            ImportId = import.Id, LineNo = lineNo + 1, RawText = $"{vote.AssemblyVoteId},{state},{ballot}", VoteId = vote.Id,
            StateCode = state, Ballot = ballot, Outcome = Outcome.Invalid, ReasonCode = reasonCode,
            ReasonMessage = "Invalid in a fixture.",
        };
        db.ImportRows.Add(row);
        await db.SaveChangesAsync(Ct);
        return row;
    }

    private static IEnumerable<string?> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString());
}
