namespace Meridian.Domain.Scoring;

/// <summary>How a ballot that is neither yes nor no counts: not at all, or as <c>Credit</c> of <c>Unit</c> points matched.</summary>
public readonly record struct Treatment(bool Counts, int Credit, int Unit)
{
    public static Treatment Excluded => new(false, 0, 1);
    public static Treatment Miss(int unit) => new(true, 0, unit);
    public static Treatment Credited(int credit, int unit) => new(true, credit, unit);
}

/// <summary>
/// A named, versioned, immutable set of scoring rules (docs/adr/0007-rule-sets-and-evidence-metrics.md).
/// It decides only how abstentions and absences count; a non-member or a member with no data never does.
/// </summary>
/// <param name="WeightUnit">Matched and counted weight are stored in points of 1 ÷ this unit of a weight.</param>
public sealed record RuleSet(string Id, string Name, string Description, int WeightUnit, Treatment Abstention, Treatment Absence);

/// <summary>Every known rule set. A rule set is never edited once a snapshot has used it; a change is a new one.</summary>
public static class RuleSets
{
    public const string DefaultId = "alignment-2026.1";

    public static readonly RuleSet Default = new(DefaultId, "Alignment 2026.1 (default)",
        "Abstentions and absences are left out.", 1, Treatment.Excluded, Treatment.Excluded);

    public static IReadOnlyList<RuleSet> All { get; } =
    [
        Default,
        new("abstain-as-miss", "Abstain as miss", "Abstentions count as not aligned; absences are left out.",
            1, Treatment.Miss(1), Treatment.Excluded),
        new("abstain-half-credit", "Abstain for half credit", "Abstentions earn half credit; absences are left out.",
            2, Treatment.Credited(1, 2), Treatment.Excluded),
        new("strict", "Strict", "Abstentions and absences both count as not aligned.",
            1, Treatment.Miss(1), Treatment.Miss(1)),
    ];

    public static bool TryGet(string id, out RuleSet ruleSet)
    {
        ruleSet = All.FirstOrDefault(r => r.Id == id)!;
        return ruleSet is not null;
    }

    public static RuleSet Get(string id) =>
        TryGet(id, out var ruleSet) ? ruleSet : throw new ArgumentException($"Unknown rule set {id}.", nameof(id));
}
