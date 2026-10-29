namespace Meridian.Domain.Scoring;

/// <summary>One fact a snapshot scores: a member state in a vote, with the vote's position.</summary>
/// <param name="Member">Whether the state was a member on the vote's approved date.</param>
/// <param name="Ballot">The state's approved country vote, or null when there is none (no data).</param>
public sealed record ScoringFact(long VoteId, string StateCode, Stance Stance, short Weight, bool Member, Ballot? Ballot);

public sealed record ScoredLine(long VoteId, string StateCode, LineCategory Category);

/// <summary>One state's result: exact integer weights, and a rank only when something was countable.</summary>
public sealed record StateResult(string StateCode, int MatchedWeight, int CountedWeight, int CountableVotes, int? Rank)
{
    /// <summary>100 × matched ÷ counted; null (n/a) when nothing was countable.</summary>
    public double? Score => CountedWeight == 0 ? null : 100.0 * MatchedWeight / CountedWeight;
}

/// <param name="Lines">One per fact, in the order given.</param>
/// <param name="Results">Ranked states by rank then code, then the n/a states by code.</param>
public sealed record ScoredSnapshot(IReadOnlyList<ScoredLine> Lines, IReadOnlyList<StateResult> Results);

/// <summary>
/// The Alignment Index (docs/adr/0006-scoring-rules.md): score = 100 × matched weight ÷
/// counted weight over a member's yes and no ballots, with how abstentions and absences count
/// decided by the rule set (docs/adr/0007-rule-sets-and-evidence-metrics.md). Weights are
/// integer points of 1 ÷ the rule set's weight unit. Pure, so every rule is unit-tested.
/// </summary>
public static class AlignmentScoring
{
    /// <summary>The default rule set's id, recorded on snapshots scored without a choice.</summary>
    public const string RulesVersion = RuleSets.DefaultId;

    public static ScoredSnapshot Score(IEnumerable<ScoringFact> facts, RuleSet? ruleSet = null)
    {
        ruleSet ??= RuleSets.Default;
        var unit = ruleSet.WeightUnit;
        var lines = new List<ScoredLine>();
        var totals = new Dictionary<string, (int Matched, int Counted, int Countable)>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            var category = Categorise(fact);
            lines.Add(new ScoredLine(fact.VoteId, fact.StateCode, category));
            var (matched, counted, countable) = totals.GetValueOrDefault(fact.StateCode);
            totals[fact.StateCode] = category switch
            {
                LineCategory.Match => (matched + fact.Weight * unit, counted + fact.Weight * unit, countable + 1),
                LineCategory.Mismatch => (matched, counted + fact.Weight * unit, countable + 1),
                LineCategory.Abstain => Counted(ruleSet.Abstention, fact.Weight, matched, counted, countable),
                LineCategory.Absent => Counted(ruleSet.Absence, fact.Weight, matched, counted, countable),
                _ => (matched, counted, countable),
            };
        }

        return new ScoredSnapshot(lines, Rank(totals));
    }

    /// <summary>An abstention or an absence adds what the rule set says; it is never a countable vote.</summary>
    private static (int, int, int) Counted(Treatment treatment, short weight, int matched, int counted, int countable) =>
        treatment.Counts ? (matched + weight * treatment.Credit, counted + weight * treatment.Unit, countable) : (matched, counted, countable);

    /// <summary>
    /// Rules 1–4: only a member's yes or no is countable. Membership comes first, so a
    /// ballot stored for a vote outside membership never counts.
    /// </summary>
    private static LineCategory Categorise(ScoringFact fact) => fact switch
    {
        { Member: false } => LineCategory.NonMember,
        { Ballot: null } => LineCategory.NoData,
        { Ballot: Ballot.A } => LineCategory.Abstain,
        { Ballot: Ballot.X } => LineCategory.Absent,
        _ => (fact.Ballot == Ballot.Y) == (fact.Stance == Stance.Yes) ? LineCategory.Match : LineCategory.Mismatch,
    };

    /// <summary>
    /// Rules 5–7: competition ranking (1, 2, 2, 4) on the exact score, compared as
    /// fractions so nothing is rounded; ties by code; nothing countable means no rank, last.
    /// </summary>
    private static List<StateResult> Rank(Dictionary<string, (int Matched, int Counted, int Countable)> totals)
    {
        var ranked = totals.Where(t => t.Value.Counted > 0)
            .Select(t => new StateResult(t.Key, t.Value.Matched, t.Value.Counted, t.Value.Countable, null))
            .ToList();
        ranked.Sort((a, b) => Compare(b, a) is var byScore and not 0 ? byScore : string.CompareOrdinal(a.StateCode, b.StateCode));
        for (var i = 0; i < ranked.Count; i++)
            ranked[i] = ranked[i] with { Rank = i > 0 && Compare(ranked[i], ranked[i - 1]) == 0 ? ranked[i - 1].Rank : i + 1 };

        var unranked = totals.Where(t => t.Value.Counted == 0)
            .Select(t => new StateResult(t.Key, 0, 0, 0, null))
            .OrderBy(r => r.StateCode, StringComparer.Ordinal);
        return [.. ranked, .. unranked];
    }

    /// <summary>a/b against c/d as a·d against c·b, exactly.</summary>
    private static int Compare(StateResult x, StateResult y) =>
        ((long)x.MatchedWeight * y.CountedWeight).CompareTo((long)y.MatchedWeight * x.CountedWeight);
}
