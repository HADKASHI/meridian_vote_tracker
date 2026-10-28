using Meridian.Domain.Imports;

namespace Meridian.Domain.Tests.Imports;

/// <summary>Map ticket 08 rules 3 to 7: one outcome per row, first match wins.</summary>
public sealed class ClassifierTests
{
    private static readonly MemberStates Members = new(new Dictionary<string, IReadOnlyList<MembershipSpan>>
    {
        ["BRA"] = [new(new DateOnly(1990, 1, 1), null)],
        ["FRA"] = [new(new DateOnly(1990, 1, 1), null)],
        ["IND"] = [new(new DateOnly(1990, 1, 1), null)],
        ["JPN"] = [new(new DateOnly(1990, 1, 1), null)],
        ["NZL"] = [new(new DateOnly(1990, 1, 1), new DateOnly(2021, 6, 30))],
    });

    private static int _line = 1;

    private static ExportRow Row(string code, string ballot, string vote = "MA/2024/101", string session = "79",
        string date = "2024-01-10", string title = "Oceans and the Law of the Sea", string? name = null)
    {
        var line = ++_line;
        return new ExportRow(line, $"{vote},{session},{date},{title},{code},{ballot}", 6, 6,
            new ExportFields(vote, session, date, title, code, ballot, name, null));
    }

    private static VoteVerdict Classify(IReadOnlyList<ExportRow> rows, ApprovedVote? approved = null) =>
        Classifier.Classify(rows, approved ?? ApprovedVote.Nothing, Members);

    [Fact]
    public void Rule3_with_nothing_approved_every_valid_row_is_new()
    {
        var verdict = Classify([Row("BRA", "Y"), Row("IND", "N")]);

        Assert.Equal([Outcome.New, Outcome.New], verdict.Rows.Select(r => r.Outcome));
        Assert.All(verdict.Rows, r => Assert.Equal(ReasonCode.NewValue, r.Reason));
        Assert.All(verdict.Rows, r => Assert.Equal(LinkKind.NewRevision, r.Ballot.Kind));
        Assert.All(verdict.Rows, r => Assert.Equal(LinkKind.NewRevision, r.Details.Kind));
        Assert.Equal(new VoteDetails(79, new DateOnly(2024, 1, 10), "Oceans and the Law of the Sea"), verdict.NewDetails);
        Assert.Equal(["BRA", "IND"], verdict.Rows.Select(r => r.Values!.StateCode));
        Assert.Equal([Ballot.Y, Ballot.N], verdict.Rows.Select(r => r.Values!.Ballot));
    }

    private static RowVerdict One(ExportRow row) => Assert.Single(Classify([row]).Rows);

    [Fact]
    public void Rule3_a_row_whose_field_count_differs_from_the_header_is_invalid()
    {
        var verdict = One(new ExportRow(9, "MA/2024/101;79,2024-01-10,A,BRA", 5, 6, null));

        Assert.Equal((Outcome.Invalid, ReasonCode.FieldCount), (verdict.Outcome, verdict.Reason));
        Assert.Equal("Wrong number of fields: 5 where the header has 6.", verdict.Message);
        Assert.Equal((LinkKind.None, LinkKind.None), (verdict.Ballot.Kind, verdict.Details.Kind));
    }

    [Theory]
    [InlineData("", "Y", "79", "2024-01-10", "Oceans")]
    [InlineData("BRA", "Y", "", "2024-01-10", "Oceans")]
    [InlineData("BRA", "Y", "79", "", "Oceans")]
    [InlineData("BRA", "Y", "79", "2024-01-10", " ")]
    public void Rule3_a_missing_required_field_is_invalid(string code, string ballot, string session, string date, string title)
    {
        var verdict = One(Row(code, ballot, session: session, date: date, title: title));

        Assert.Equal((Outcome.Invalid, ReasonCode.MissingField), (verdict.Outcome, verdict.Reason));
    }

    [Theory]
    [InlineData("FRN")]
    [InlineData("FR")]
    [InlineData("XYZ")]
    public void Rule3_an_unknown_country_code_is_invalid_and_never_guessed(string code)
    {
        var verdict = One(Row(code, "X"));

        Assert.Equal((Outcome.Invalid, ReasonCode.UnknownCountryCode), (verdict.Outcome, verdict.Reason));
        Assert.Equal(code, verdict.Values!.StateCode);
    }

    [Theory]
    [InlineData("Yes")]
    [InlineData("no")]
    [InlineData("YN")]
    [InlineData("Z")]
    public void Rule3_a_ballot_that_is_not_y_n_a_or_x_is_invalid_and_words_are_never_mapped(string ballot)
    {
        var verdict = One(Row("BRA", ballot));

        Assert.Equal((Outcome.Invalid, ReasonCode.InvalidBallot), (verdict.Outcome, verdict.Reason));
        Assert.Equal(ballot, verdict.Values!.BallotText);
        Assert.Null(verdict.Values.Ballot);
    }

    [Fact]
    public void Rule3_an_empty_ballot_is_invalid_and_never_read_as_absent()
    {
        var verdict = One(Row("BRA", ""));

        Assert.Equal((Outcome.Invalid, ReasonCode.EmptyBallot), (verdict.Outcome, verdict.Reason));
        Assert.Null(verdict.Values!.Ballot);
    }

    [Fact]
    public void Rule3_an_invalid_date_is_invalid()
    {
        var verdict = One(Row("BRA", "Y", date: "02/21/2024"));

        Assert.Equal((Outcome.Invalid, ReasonCode.InvalidDate), (verdict.Outcome, verdict.Reason));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("79.0")]
    [InlineData("seventy")]
    [InlineData("+79")]
    public void Rule3_a_session_that_is_not_a_positive_integer_is_invalid(string session)
    {
        var verdict = One(Row("BRA", "Y", session: session));

        Assert.Equal((Outcome.Invalid, ReasonCode.InvalidSession), (verdict.Outcome, verdict.Reason));
    }

    [Fact]
    public void Rule3_a_state_that_was_not_member_on_date_on_the_vote_date_is_invalid()
    {
        var verdict = One(Row("NZL", "Y", date: "2024-01-10"));

        Assert.Equal((Outcome.Invalid, ReasonCode.NotMemberOnDate), (verdict.Outcome, verdict.Reason));
        Assert.Equal("NZL was not a member on 2024-01-10.", verdict.Message);
    }

    [Theory]
    [InlineData("1990-01-01", Outcome.New)]
    [InlineData("2021-06-30", Outcome.New)]
    [InlineData("2021-07-01", Outcome.Invalid)]
    [InlineData("1989-12-31", Outcome.Invalid)]
    public void Rule12_membership_is_inclusive_at_both_ends(string date, Outcome expected)
    {
        Assert.Equal(expected, One(Row("NZL", "Y", date: date)).Outcome);
    }

    [Fact]
    public void Rule12_an_open_membership_means_still_a_member()
    {
        Assert.Equal(Outcome.New, One(Row("BRA", "Y", date: "2099-12-31")).Outcome);
    }

    [Fact]
    public void Rule3_normalisations_are_recorded_on_the_verdict()
    {
        var verdict = One(Row("bra", "y", date: "10/01/2024"));

        Assert.Equal(Outcome.New, verdict.Outcome);
        Assert.Equal(["country_code: upper-cased", "ballot: upper-cased", "vote_date: 10/01/2024 read as 2024-01-10"], verdict.Normalisations);
        Assert.Equal(new DateOnly(2024, 1, 10), verdict.Values!.VoteDate);
    }
}
