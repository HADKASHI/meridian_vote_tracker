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

    private static Outcome[] Outcomes(VoteVerdict verdict) => verdict.Rows.Select(r => r.Outcome).ToArray();

    [Fact]
    public void Rule3_rows_giving_one_state_different_ballots_are_all_contradictions()
    {
        var verdict = Classify([Row("FRA", "Y"), Row("BRA", "Y"), Row("FRA", "Y"), Row("FRA", "N")]);

        Assert.Equal([Outcome.Contradiction, Outcome.New, Outcome.Contradiction, Outcome.Contradiction], Outcomes(verdict));
        Assert.All(verdict.Rows.Where(r => r.Outcome == Outcome.Contradiction), r =>
        {
            Assert.Equal(ReasonCode.BallotContradiction, r.Reason);
            Assert.Equal(LinkKind.None, r.Ballot.Kind);
        });
        Assert.Contains("FRA", verdict.Rows[0].Message);
    }

    [Fact]
    public void Rule3_an_exact_repeat_is_a_duplicate_and_the_first_copy_is_classified_normally()
    {
        var first = Row("JPN", "Y");
        var verdict = Classify([first, Row("BRA", "N"), Row("JPN", "Y")]);

        Assert.Equal([Outcome.New, Outcome.New, Outcome.Duplicate], Outcomes(verdict));
        Assert.Equal(ReasonCode.Duplicate, verdict.Rows[2].Reason);
        Assert.Equal($"Identical to line {first.LineNo}.", verdict.Rows[2].Message);
        Assert.Equal(LinkKind.None, verdict.Rows[2].Ballot.Kind);
    }

    [Fact]
    public void Rule3_a_duplicate_ignores_country_name_published_at_and_formatting()
    {
        var verdict = Classify([Row("JPN", "Y", name: "Japan"), Row(" jpn", "y", name: "Nippon", title: "OCEANS and the  law of the sea")]);

        Assert.Equal([Outcome.New, Outcome.Duplicate], Outcomes(verdict));
    }

    [Fact]
    public void Rule3_an_invalid_row_never_contradicts_or_duplicates_a_valid_one()
    {
        var verdict = Classify([Row("FRA", "Y"), Row("FRA", "Yes"), Row("FRA", "")]);

        Assert.Equal([Outcome.New, Outcome.Invalid, Outcome.Invalid], Outcomes(verdict));
    }

    [Fact]
    public void Rule3_invalid_comes_before_contradiction_and_contradiction_before_duplicate()
    {
        var verdict = Classify([Row("FRA", "Y"), Row("FRA", "Y"), Row("FRA", "N"), Row("FRN", "N")]);

        Assert.Equal([Outcome.Contradiction, Outcome.Contradiction, Outcome.Contradiction, Outcome.Invalid], Outcomes(verdict));
    }

    [Theory]
    [InlineData("80", "2024-01-10", "Oceans and the Law of the Sea")]
    [InlineData("79", "2024-01-11", "Oceans and the Law of the Sea")]
    [InlineData("79", "2024-01-10", "Oceans and the Law of the Seas")]
    public void Rule4_rows_disagreeing_on_session_date_or_title_hold_back_the_whole_vote(string session, string date, string title)
    {
        var verdict = Classify([Row("BRA", "Y"), Row("IND", "N"), Row("JPN", "A", session: session, date: date, title: title), Row("FRN", "Y")]);

        Assert.Equal([Outcome.Contradiction, Outcome.Contradiction, Outcome.Contradiction, Outcome.Invalid], Outcomes(verdict));
        Assert.All(verdict.Rows.Take(3), r =>
        {
            Assert.Equal(ReasonCode.DetailsContradiction, r.Reason);
            Assert.Equal((LinkKind.None, LinkKind.None), (r.Ballot.Kind, r.Details.Kind));
        });
        Assert.Null(verdict.NewDetails);
    }

    [Fact]
    public void Rule4_the_reason_is_given_once_and_the_other_rows_point_to_it()
    {
        var first = Row("BRA", "Y");
        var verdict = Classify([first, Row("IND", "N", date: "2024-01-11"), Row("JPN", "A")]);

        Assert.Contains("vote_date", verdict.Rows[0].Message);
        Assert.Equal($"Held back with its vote: see line {first.LineNo}.", verdict.Rows[1].Message);
        Assert.Equal($"Held back with its vote: see line {first.LineNo}.", verdict.Rows[2].Message);
    }

    [Fact]
    public void Rule4_titles_differing_only_in_case_spacing_quotes_or_dashes_agree()
    {
        var verdict = Classify([Row("BRA", "Y", title: "States’ Duties — Review"), Row("IND", "N", title: "states'  duties - review")]);

        Assert.Equal([Outcome.New, Outcome.New], Outcomes(verdict));
        Assert.Equal("States’ Duties — Review", verdict.NewDetails!.Title);
    }
}
