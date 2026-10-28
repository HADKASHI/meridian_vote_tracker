using Meridian.Domain.Scoring;
using static Meridian.Domain.Ballot;

namespace Meridian.Domain.Tests.Scoring;

/// <summary>The scoring rules of map ticket 05 (docs/adr/0006-scoring-rules.md), one named test per rule.</summary>
public sealed class AlignmentScoringTests
{
    private static ScoringFact Fact(long vote, string state, Ballot? ballot, Stance stance = Stance.Yes,
        short weight = 1, bool member = true) =>
        new(vote, state, stance, weight, member, ballot);

    private static StateResult Result(ScoringOutcome outcome, string state) =>
        outcome.Results.Single(r => r.StateCode == state);

    [Fact]
    public void A_ballot_matching_the_stance_adds_its_weight_to_both_and_a_differing_one_to_counted_only()
    {
        var outcome = AlignmentScoring.Score([
            Fact(1, "FRA", Y, Stance.Yes, weight: 3),
            Fact(2, "FRA", Y, Stance.No, weight: 2),
            Fact(3, "FRA", N, Stance.No, weight: 5),
        ]);

        Assert.Equal([LineCategory.Match, LineCategory.Mismatch, LineCategory.Match], outcome.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("FRA", 8, 10, 3, 1), Result(outcome, "FRA"));
        Assert.Equal(80.0, Result(outcome, "FRA").Score);
    }

    [Fact]
    public void Rule_1_an_abstention_is_labelled_abstain_and_not_counted()
    {
        var outcome = AlignmentScoring.Score([Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", A, weight: 5)]);

        Assert.Equal(LineCategory.Abstain, outcome.Lines[1].Category);
        Assert.Equal(new StateResult("FRA", 2, 2, 1, 1), Result(outcome, "FRA"));
    }

    [Fact]
    public void Rule_2_an_absence_is_labelled_absent_and_not_counted()
    {
        var outcome = AlignmentScoring.Score([Fact(1, "FRA", N, weight: 2), Fact(2, "FRA", X, weight: 5)]);

        Assert.Equal(LineCategory.Absent, outcome.Lines[1].Category);
        Assert.Equal(new StateResult("FRA", 0, 2, 1, 1), Result(outcome, "FRA"));
    }

    [Fact]
    public void Rule_3_a_vote_outside_membership_is_labelled_non_member_and_not_counted_whatever_ballot_is_stored()
    {
        var outcome = AlignmentScoring.Score([
            Fact(1, "ISL", Y, weight: 4),
            Fact(2, "ISL", Y, weight: 5, member: false),
            Fact(3, "ISL", null, weight: 5, member: false),
        ]);

        Assert.Equal([LineCategory.Match, LineCategory.NonMember, LineCategory.NonMember], outcome.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("ISL", 4, 4, 1, 1), Result(outcome, "ISL"));
    }

    [Fact]
    public void Rule_4_a_member_with_no_country_vote_is_no_data_kept_apart_from_absent_and_not_counted()
    {
        var outcome = AlignmentScoring.Score([Fact(1, "BRA", N, Stance.No, 3), Fact(2, "BRA", null, weight: 5), Fact(3, "BRA", X)]);

        Assert.Equal([LineCategory.Match, LineCategory.NoData, LineCategory.Absent], outcome.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("BRA", 3, 3, 1, 1), Result(outcome, "BRA"));
    }

    [Fact]
    public void Rule_5_a_state_with_nothing_countable_is_na_unranked_and_listed_after_every_ranked_state()
    {
        var outcome = AlignmentScoring.Score([
            Fact(1, "ARG", A), Fact(1, "BRA", N), Fact(1, "CIV", null), Fact(1, "FRA", Y),
        ]);

        Assert.Equal(["FRA", "BRA", "ARG", "CIV"], outcome.Results.Select(r => r.StateCode));
        var argentina = Result(outcome, "ARG");
        Assert.Equal((0, 0, 0, null, null), (argentina.MatchedWeight, argentina.CountedWeight, argentina.CountableVotes,
            argentina.Rank, argentina.Score));
        Assert.Equal(0.0, Result(outcome, "BRA").Score);
        Assert.Equal(2, Result(outcome, "BRA").Rank);
    }

    [Fact]
    public void Rule_6_weights_are_exact_integers_so_equal_fractions_tie()
    {
        // ARG: 2 of 3. JPN: 4 + 2 of 9. Both are exactly two thirds.
        var outcome = AlignmentScoring.Score([
            Fact(1, "ARG", Y, weight: 2), Fact(2, "ARG", N, weight: 1),
            Fact(1, "JPN", Y, weight: 4), Fact(2, "JPN", Y, weight: 2), Fact(3, "JPN", N, weight: 3),
        ]);

        Assert.Equal(new StateResult("ARG", 2, 3, 2, 1), Result(outcome, "ARG"));
        Assert.Equal(new StateResult("JPN", 6, 9, 3, 1), Result(outcome, "JPN"));
        Assert.Equal(100.0 * 2 / 3, Result(outcome, "ARG").Score);
    }

    [Fact]
    public void Rule_6_scores_that_round_alike_but_differ_exactly_get_different_ranks()
    {
        // 667 of 1000 (66.7%) is above 2 of 3 (66.666…%), though both display as 66.7.
        var outcome = AlignmentScoring.Score([
            Fact(1, "ARG", Y, weight: 2), Fact(2, "ARG", N, weight: 1),
            .. Enumerable.Range(1, 1000).Select(vote => Fact(100 + vote, "BRA", vote <= 667 ? Y : N)),
        ]);

        Assert.Equal((1, 2), (Result(outcome, "BRA").Rank!.Value, Result(outcome, "ARG").Rank!.Value));
    }

    [Fact]
    public void Rule_7_equal_scores_share_a_rank_the_next_is_skipped_and_a_tie_is_listed_by_code()
    {
        var outcome = AlignmentScoring.Score([
            Fact(1, "TUR", Y), Fact(1, "JPN", Y), Fact(2, "JPN", N),
            Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", N, weight: 2),
            Fact(1, "ARG", N),
        ]);

        Assert.Equal([("TUR", 1), ("FRA", 2), ("JPN", 2), ("ARG", 4)],
            outcome.Results.Select(r => (r.StateCode, r.Rank!.Value)));
    }

    [Fact]
    public void Rule_7_states_with_nothing_countable_are_listed_by_code()
    {
        var outcome = AlignmentScoring.Score([Fact(1, "TUR", X), Fact(1, "BRA", null), Fact(1, "MNE", A, member: false)]);

        Assert.Equal(["BRA", "MNE", "TUR"], outcome.Results.Select(r => r.StateCode));
    }

    [Fact]
    public void Rule_8_one_countable_vote_is_enough_to_be_ranked_and_the_basis_is_kept()
    {
        var outcome = AlignmentScoring.Score([
            Fact(1, "SSD", Y, weight: 1), Fact(2, "SSD", A, weight: 5),
            Fact(1, "FRA", Y, weight: 1), Fact(2, "FRA", N, Stance.Yes, weight: 5),
        ]);

        Assert.Equal(new StateResult("SSD", 1, 1, 1, 1), Result(outcome, "SSD"));
        Assert.Equal(new StateResult("FRA", 1, 6, 2, 2), Result(outcome, "FRA"));
    }

    [Fact]
    public void The_scoring_rules_version_is_fixed()
    {
        Assert.Equal("alignment-2026.1", AlignmentScoring.RulesVersion);
    }
}
