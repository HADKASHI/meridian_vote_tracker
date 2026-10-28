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

    private static RowVerdict One(ExportRow row, ApprovedVote? approved = null) => Assert.Single(Classify([row], approved).Rows);

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

    private static readonly VoteDetails Approved101 = new(79, new DateOnly(2024, 1, 10), "Oceans and the Law of the Sea");

    private static ApprovedVote Approved(IReadOnlyList<KnownConflict>? conflicts = null, VoteDetails? details = null,
        params (string State, Ballot Ballot, long Revision)[] ballots) =>
        new(new Approved<VoteDetails>(500, details ?? Approved101),
            ballots.ToDictionary(b => b.State, b => new Approved<Ballot>(b.Revision, b.Ballot)),
            conflicts ?? []);

    [Fact]
    public void Rule5_a_row_matching_approved_data_is_unchanged_and_links_the_approved_revisions()
    {
        var verdict = One(Row("BRA", "Y"), Approved(ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.Unchanged, ReasonCode.MatchesApproved), (verdict.Outcome, verdict.Reason));
        Assert.Equal(Link.ToApproved(501), verdict.Ballot);
        Assert.Equal(Link.ToApproved(500), verdict.Details);
    }

    [Fact]
    public void Rule5_a_ballot_with_nothing_approved_is_new_under_the_approved_details()
    {
        var verdict = Classify([Row("FRA", "N")], Approved(ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.New, LinkKind.NewRevision, LinkKind.Approved), (verdict.Rows[0].Outcome, verdict.Rows[0].Ballot.Kind, verdict.Rows[0].Details.Kind));
        Assert.Null(verdict.NewDetails);
    }

    [Fact]
    public void Rule5_a_ballot_that_differs_from_approved_data_opens_a_conflict()
    {
        var verdict = One(Row("BRA", "A"), Approved(ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.Conflict, ReasonCode.DiffersFromApproved), (verdict.Outcome, verdict.Reason));
        Assert.Equal(LinkKind.NewConflict, verdict.Ballot.Kind);
        Assert.Equal(Link.ToApproved(500), verdict.Details);
        Assert.Equal("Ballot A differs from the approved Y; a conflict is opened and Y stays approved.", verdict.Message);
    }

    [Fact]
    public void Rule5_comparisons_ignore_country_name_and_title_formatting()
    {
        var verdict = One(Row("BRA", "Y", name: "Brasil", title: "OCEANS and the law  of the sea"), Approved(ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal(Outcome.Unchanged, verdict.Outcome);
    }

    [Fact]
    public void Rule5_changed_details_open_one_conflict_for_the_vote_that_every_row_links_to()
    {
        var verdict = Classify([Row("BRA", "Y", title: "Oceans"), Row("FRA", "N", title: "Oceans")],
            Approved(ballots: [("BRA", Ballot.Y, 501), ("FRA", Ballot.N, 502)]));

        Assert.All(verdict.Rows, r =>
        {
            Assert.Equal((Outcome.Conflict, ReasonCode.DiffersFromApproved), (r.Outcome, r.Reason));
            Assert.Equal(LinkKind.NewConflict, r.Details.Kind);
            Assert.Equal(LinkKind.Approved, r.Ballot.Kind);
        });
        Assert.Equal(Approved101 with { Title = "Oceans" }, verdict.NewDetailsConflict);
        Assert.Null(verdict.NewDetails);
    }

    [Fact]
    public void Rule5_a_row_with_new_details_still_has_its_ballot_compared_on_its_own()
    {
        var verdict = Classify([Row("BRA", "A", title: "Oceans"), Row("JPN", "Y", title: "Oceans")],
            Approved(ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((LinkKind.NewConflict, LinkKind.NewConflict), (verdict.Rows[0].Details.Kind, verdict.Rows[0].Ballot.Kind));
        Assert.Equal((LinkKind.NewConflict, LinkKind.NewRevision), (verdict.Rows[1].Details.Kind, verdict.Rows[1].Ballot.Kind));
        Assert.Equal(Outcome.Conflict, verdict.Rows[1].Outcome);
    }

    [Fact]
    public void Rule5_membership_of_a_row_with_new_details_is_checked_against_the_approved_date()
    {
        var approvedBefore = Approved101 with { VoteDate = new DateOnly(2021, 6, 1) };

        var verdict = One(Row("NZL", "Y", date: "2024-01-10"), Approved(details: approvedBefore));

        Assert.Equal(LinkKind.NewRevision, verdict.Ballot.Kind);
        Assert.Equal(LinkKind.NewConflict, verdict.Details.Kind);
        Assert.Equal(Outcome.Invalid, One(Row("NZL", "Y", date: "2021-06-01"), Approved(details: Approved101)).Outcome);
    }

    [Fact]
    public void Rule7_a_value_equal_to_a_pending_conflict_links_to_it()
    {
        var pending = new KnownConflict(77, ConflictSubject.Ballot, 501, "BRA", Ballot.A, null, null);

        var verdict = One(Row("BRA", "A"), Approved([pending], ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.Conflict, ReasonCode.PendingConflict), (verdict.Outcome, verdict.Reason));
        Assert.Equal(Link.ToConflict(77), verdict.Ballot);
    }

    [Fact]
    public void Rule7_changed_details_equal_to_a_pending_details_conflict_link_to_it()
    {
        var pending = new KnownConflict(78, ConflictSubject.Details, 500, null, null, Approved101 with { Title = "Oceans" }, null);

        var verdict = One(Row("BRA", "Y", title: "OCEANS"), Approved([pending], ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal(Link.ToConflict(78), verdict.Details);
        Assert.Equal(ReasonCode.PendingConflict, verdict.Reason);
    }

    [Fact]
    public void Rule7_a_value_already_rejected_never_reopens()
    {
        var rejected = new KnownConflict(79, ConflictSubject.Ballot, 501, "BRA", Ballot.A, null, Decision.Reject);

        var verdict = One(Row("BRA", "A"), Approved([rejected], ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.Conflict, ReasonCode.PreviouslyRejected), (verdict.Outcome, verdict.Reason));
        Assert.Equal(Link.ToConflict(79), verdict.Ballot);
    }

    [Fact]
    public void Rule7_a_conflict_compared_against_an_older_revision_does_not_match()
    {
        var old = new KnownConflict(80, ConflictSubject.Ballot, 400, "BRA", Ballot.A, null, Decision.Reject);

        var verdict = One(Row("BRA", "A"), Approved([old], ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal(LinkKind.NewConflict, verdict.Ballot.Kind);
    }

    [Fact]
    public void Rule7_a_different_incoming_value_on_the_same_subject_opens_its_own_conflict()
    {
        var pending = new KnownConflict(77, ConflictSubject.Ballot, 501, "BRA", Ballot.A, null, null);

        var verdict = One(Row("BRA", "N"), Approved([pending], ballots: ("BRA", Ballot.Y, 501)));

        Assert.Equal((Outcome.Conflict, ReasonCode.DiffersFromApproved, LinkKind.NewConflict), (verdict.Outcome, verdict.Reason, verdict.Ballot.Kind));
    }

    [Fact]
    public void Rule7_a_value_equal_to_the_approved_one_is_unchanged_even_while_a_conflict_is_pending()
    {
        var pending = new KnownConflict(77, ConflictSubject.Ballot, 501, "BRA", Ballot.A, null, null);

        Assert.Equal(Outcome.Unchanged, One(Row("BRA", "Y"), Approved([pending], ballots: ("BRA", Ballot.Y, 501))).Outcome);
    }

    [Fact]
    public void Rule11_members_on_the_vote_date_with_no_approved_ballot_after_the_import_are_missing()
    {
        var verdict = Classify([Row("BRA", "Y"), Row("FRA", "Yes")]);

        Assert.Equal(["FRA", "IND", "JPN"], Classifier.MissingMembers(verdict, ApprovedVote.Nothing, Members));
    }

    [Fact]
    public void Rule11_approved_ballots_count_and_non_members_are_not_missing()
    {
        var approved = Approved(ballots: [("IND", Ballot.N, 502), ("JPN", Ballot.A, 503)]);

        var verdict = Classify([Row("BRA", "Y")], approved);

        Assert.Equal(["FRA"], Classifier.MissingMembers(verdict, approved, Members));
    }

    [Fact]
    public void Rule11_a_vote_with_no_details_has_no_date_to_be_incomplete_on()
    {
        var verdict = Classify([Row("BRA", "Y"), Row("IND", "N", date: "2024-01-11")]);

        Assert.Empty(Classifier.MissingMembers(verdict, ApprovedVote.Nothing, Members));
    }
}
