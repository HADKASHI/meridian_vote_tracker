using Meridian.Domain.Scoring;
using static Meridian.Domain.Ballot;

namespace Meridian.Domain.Tests.Scoring;

/// <summary>The scoring rules of map ticket 05 (docs/adr/0006-scoring-rules.md), one named test per rule.</summary>
public sealed class AlignmentScoringTests
{
    private static ScoringFact Fact(long vote, string state, Ballot? ballot, Stance stance = Stance.Yes,
        short weight = 1, bool member = true) =>
        new(vote, state, stance, weight, member, ballot);

    private static StateResult Result(ScoredSnapshot scored, string state) =>
        scored.Results.Single(r => r.StateCode == state);

    [Fact]
    public void A_ballot_matching_the_stance_adds_its_weight_to_both_and_a_differing_one_to_counted_only()
    {
        var scored = AlignmentScoring.Score([
            Fact(1, "FRA", Y, Stance.Yes, weight: 3),
            Fact(2, "FRA", Y, Stance.No, weight: 2),
            Fact(3, "FRA", N, Stance.No, weight: 5),
        ]);

        Assert.Equal([LineCategory.Match, LineCategory.Mismatch, LineCategory.Match], scored.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("FRA", 8, 10, 3, 1), Result(scored, "FRA"));
        Assert.Equal(80.0, Result(scored, "FRA").Score);
    }

    [Fact]
    public void Rule_1_an_abstention_is_labelled_abstain_and_not_counted()
    {
        var scored = AlignmentScoring.Score([Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", A, weight: 5)]);

        Assert.Equal(LineCategory.Abstain, scored.Lines[1].Category);
        Assert.Equal(new StateResult("FRA", 2, 2, 1, 1), Result(scored, "FRA"));
    }

    [Fact]
    public void Rule_2_an_absence_is_labelled_absent_and_not_counted()
    {
        var scored = AlignmentScoring.Score([Fact(1, "FRA", N, weight: 2), Fact(2, "FRA", X, weight: 5)]);

        Assert.Equal(LineCategory.Absent, scored.Lines[1].Category);
        Assert.Equal(new StateResult("FRA", 0, 2, 1, 1), Result(scored, "FRA"));
    }

    [Fact]
    public void Rule_3_a_vote_outside_membership_is_labelled_non_member_and_not_counted_whatever_ballot_is_stored()
    {
        var scored = AlignmentScoring.Score([
            Fact(1, "ISL", Y, weight: 4),
            Fact(2, "ISL", Y, weight: 5, member: false),
            Fact(3, "ISL", null, weight: 5, member: false),
        ]);

        Assert.Equal([LineCategory.Match, LineCategory.NonMember, LineCategory.NonMember], scored.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("ISL", 4, 4, 1, 1), Result(scored, "ISL"));
    }

    [Fact]
    public void Rule_4_a_member_with_no_country_vote_is_no_data_kept_apart_from_absent_and_not_counted()
    {
        var scored = AlignmentScoring.Score([Fact(1, "BRA", N, Stance.No, 3), Fact(2, "BRA", null, weight: 5), Fact(3, "BRA", X)]);

        Assert.Equal([LineCategory.Match, LineCategory.NoData, LineCategory.Absent], scored.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("BRA", 3, 3, 1, 1), Result(scored, "BRA"));
    }

    [Fact]
    public void Rule_5_a_state_with_nothing_countable_is_na_unranked_and_listed_after_every_ranked_state()
    {
        var scored = AlignmentScoring.Score([
            Fact(1, "ARG", A), Fact(1, "BRA", N), Fact(1, "CIV", null), Fact(1, "FRA", Y),
        ]);

        Assert.Equal(["FRA", "BRA", "ARG", "CIV"], scored.Results.Select(r => r.StateCode));
        var argentina = Result(scored, "ARG");
        Assert.Equal((0, 0, 0, null, null), (argentina.MatchedWeight, argentina.CountedWeight, argentina.CountableVotes,
            argentina.Rank, argentina.Score));
        Assert.Equal(0.0, Result(scored, "BRA").Score);
        Assert.Equal(2, Result(scored, "BRA").Rank);
    }

    [Fact]
    public void Rule_6_weights_are_exact_integers_so_equal_fractions_tie()
    {
        // ARG: 2 of 3. JPN: 4 + 2 of 9. Both are exactly two thirds.
        var scored = AlignmentScoring.Score([
            Fact(1, "ARG", Y, weight: 2), Fact(2, "ARG", N, weight: 1),
            Fact(1, "JPN", Y, weight: 4), Fact(2, "JPN", Y, weight: 2), Fact(3, "JPN", N, weight: 3),
        ]);

        Assert.Equal(new StateResult("ARG", 2, 3, 2, 1), Result(scored, "ARG"));
        Assert.Equal(new StateResult("JPN", 6, 9, 3, 1), Result(scored, "JPN"));
        Assert.Equal(100.0 * 2 / 3, Result(scored, "ARG").Score);
    }

    [Fact]
    public void Rule_6_scores_that_round_alike_but_differ_exactly_get_different_ranks()
    {
        // 667 of 1000 (66.7%) is above 2 of 3 (66.666…%), though both display as 66.7.
        var scored = AlignmentScoring.Score([
            Fact(1, "ARG", Y, weight: 2), Fact(2, "ARG", N, weight: 1),
            .. Enumerable.Range(1, 1000).Select(vote => Fact(100 + vote, "BRA", vote <= 667 ? Y : N)),
        ]);

        Assert.Equal((1, 2), (Result(scored, "BRA").Rank!.Value, Result(scored, "ARG").Rank!.Value));
    }

    [Fact]
    public void Rule_7_equal_scores_share_a_rank_the_next_is_skipped_and_a_tie_is_listed_by_code()
    {
        var scored = AlignmentScoring.Score([
            Fact(1, "TUR", Y), Fact(1, "JPN", Y), Fact(2, "JPN", N),
            Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", N, weight: 2),
            Fact(1, "ARG", N),
        ]);

        Assert.Equal([("TUR", 1), ("FRA", 2), ("JPN", 2), ("ARG", 4)],
            scored.Results.Select(r => (r.StateCode, r.Rank!.Value)));
    }

    [Fact]
    public void Rule_7_states_with_nothing_countable_are_listed_by_code()
    {
        var scored = AlignmentScoring.Score([Fact(1, "TUR", X), Fact(1, "BRA", null), Fact(1, "MNE", A, member: false)]);

        Assert.Equal(["BRA", "MNE", "TUR"], scored.Results.Select(r => r.StateCode));
    }

    [Fact]
    public void Rule_8_one_countable_vote_is_enough_to_be_ranked_and_the_basis_is_kept()
    {
        var scored = AlignmentScoring.Score([
            Fact(1, "SSD", Y, weight: 1), Fact(2, "SSD", A, weight: 5),
            Fact(1, "FRA", Y, weight: 1), Fact(2, "FRA", N, Stance.Yes, weight: 5),
        ]);

        Assert.Equal(new StateResult("SSD", 1, 1, 1, 1), Result(scored, "SSD"));
        Assert.Equal(new StateResult("FRA", 1, 6, 2, 2), Result(scored, "FRA"));
    }

    private static ScoredSnapshot Under(string ruleSetId, params ScoringFact[] facts) =>
        AlignmentScoring.Score(facts, RuleSets.Get(ruleSetId));

    [Fact]
    public void Rule_set_the_default_is_alignment_2026_1_and_scores_as_the_plain_rules_do()
    {
        Assert.Equal("alignment-2026.1", RuleSets.Default.Id);
        Assert.Equal(1, RuleSets.Default.WeightUnit);
        var facts = new[] { Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", A, weight: 5), Fact(3, "FRA", X, weight: 4) };

        Assert.Equal(AlignmentScoring.Score(facts).Results, Under("alignment-2026.1", facts).Results);
        Assert.Equal(new StateResult("FRA", 2, 2, 1, 1), Result(Under("alignment-2026.1", facts), "FRA"));
    }

    [Fact]
    public void Rule_set_abstain_as_miss_counts_an_abstention_as_not_aligned_and_leaves_an_absence_out()
    {
        var scored = Under("abstain-as-miss", Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", A, weight: 3), Fact(3, "FRA", X, weight: 4));

        Assert.Equal(new StateResult("FRA", 2, 5, 1, 1), Result(scored, "FRA"));
        Assert.Equal([LineCategory.Match, LineCategory.Abstain, LineCategory.Absent], scored.Lines.Select(l => l.Category));
    }

    [Fact]
    public void Rule_set_abstain_half_credit_stores_weights_as_integer_points_of_half_a_weight()
    {
        var rules = RuleSets.Get("abstain-half-credit");
        var scored = AlignmentScoring.Score([Fact(1, "FRA", Y, weight: 3), Fact(2, "FRA", A, weight: 1), Fact(3, "FRA", X, weight: 4)], rules);

        Assert.Equal(2, rules.WeightUnit);
        Assert.Equal(new StateResult("FRA", 7, 8, 1, 1), Result(scored, "FRA"));
        Assert.Equal(100.0 * 7 / 8, Result(scored, "FRA").Score);
    }

    [Fact]
    public void Rule_set_strict_counts_abstentions_and_absences_as_not_aligned()
    {
        var scored = Under("strict", Fact(1, "FRA", Y, weight: 2), Fact(2, "FRA", A, weight: 3), Fact(3, "FRA", X, weight: 4));

        Assert.Equal(new StateResult("FRA", 2, 9, 1, 1), Result(scored, "FRA"));
    }

    [Theory]
    [InlineData("alignment-2026.1")]
    [InlineData("abstain-as-miss")]
    [InlineData("abstain-half-credit")]
    [InlineData("strict")]
    public void Rule_set_a_non_member_and_a_member_with_no_data_are_never_counted(string ruleSetId)
    {
        var scored = Under(ruleSetId, Fact(1, "ISL", Y, weight: 4), Fact(2, "ISL", A, weight: 5, member: false),
            Fact(3, "ISL", null, weight: 5), Fact(4, "ISL", null, weight: 3, member: false));
        var unit = RuleSets.Get(ruleSetId).WeightUnit;

        Assert.Equal([LineCategory.Match, LineCategory.NonMember, LineCategory.NoData, LineCategory.NonMember], scored.Lines.Select(l => l.Category));
        Assert.Equal(new StateResult("ISL", 4 * unit, 4 * unit, 1, 1), Result(scored, "ISL"));
    }

    [Fact]
    public void Rule_set_ranking_compares_exact_fractions_in_points_so_a_half_credit_tie_is_a_tie()
    {
        // FRA: a match (2 of 2 points) and a half-credit abstention (1 of 2) = 3 of 4. BRA: a match and a miss = 2 of 4.
        var scored = Under("abstain-half-credit",
            Fact(1, "FRA", Y), Fact(2, "FRA", A),
            Fact(1, "BRA", Y), Fact(2, "BRA", N, Stance.Yes));

        Assert.Equal(new StateResult("FRA", 3, 4, 1, 1), Result(scored, "FRA"));
        Assert.Equal(new StateResult("BRA", 2, 4, 2, 2), Result(scored, "BRA"));
        // Both end on 5 of 8 points by different routes.
        var tie = Under("abstain-half-credit", Fact(1, "FRA", Y), Fact(2, "FRA", A), Fact(1, "BRA", Y), Fact(2, "BRA", Y), Fact(3, "BRA", N, Stance.Yes),
            Fact(3, "FRA", Y), Fact(4, "FRA", N, Stance.Yes), Fact(4, "BRA", A));
        Assert.Equal(Result(tie, "FRA").Rank, Result(tie, "BRA").Rank);
        Assert.Equal(Result(tie, "FRA").Score, Result(tie, "BRA").Score);
    }

    [Fact]
    public void Rule_set_a_state_with_only_abstentions_under_strict_counts_weight_and_is_ranked_with_no_countable_vote()
    {
        var scored = Under("strict", Fact(1, "FRA", A, weight: 2));

        Assert.Equal(new StateResult("FRA", 0, 2, 0, 1), Result(scored, "FRA"));
    }

    [Fact]
    public void Rule_set_an_unknown_id_is_not_found()
    {
        Assert.False(RuleSets.TryGet("alignment-2020", out _));
        Assert.Throws<ArgumentException>(() => RuleSets.Get("alignment-2020"));
        Assert.Equal(["alignment-2026.1", "abstain-as-miss", "abstain-half-credit", "strict"], RuleSets.All.Select(r => r.Id));
    }

    [Fact]
    public void The_scoring_rules_version_is_fixed()
    {
        Assert.Equal("alignment-2026.1", AlignmentScoring.RulesVersion);
    }
}
