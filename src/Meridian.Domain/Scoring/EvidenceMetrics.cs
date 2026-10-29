namespace Meridian.Domain.Scoring;

/// <summary>How well the voting evidence supports a score. It never changes the rank.</summary>
public enum Confidence { High, Medium, Low }

/// <summary>A state's stored lines in one snapshot, counted by category. Non-member lines are not part of the evidence.</summary>
public readonly record struct LineCounts(int Match, int Mismatch, int Abstain, int Absent, int NoData);

/// <summary>
/// Shares of the votes the state was a member for. <c>Confidence</c> is null when nothing was countable,
/// and every share is null when the state was never a member.
/// </summary>
public sealed record Evidence(int MemberVotes, double? Completeness, double? AbstentionRate, double? MissingData, Confidence? Confidence);

/// <summary>
/// ADR 0007: derived from the stored lines, so the same under every rule set and available for old snapshots.
/// Thresholds are compared as integers so a share of exactly 90% or 70% is not lost to rounding.
/// </summary>
public static class EvidenceMetrics
{
    private const int HighPercent = 90, HighVotes = 10;
    private const int MediumPercent = 70, MediumVotes = 5;

    public static Evidence From(LineCounts counts)
    {
        var countable = counts.Match + counts.Mismatch;
        var members = countable + counts.Abstain + counts.Absent + counts.NoData;
        if (members == 0) return new Evidence(0, null, null, null, null);

        return new Evidence(members, (double)countable / members, (double)counts.Abstain / members, (double)counts.NoData / members,
            BandOf(countable, members));
    }

    private static Confidence? BandOf(int countable, int members) =>
        countable == 0 ? null
        : countable * 100 >= HighPercent * members && countable >= HighVotes ? Confidence.High
        : countable * 100 >= MediumPercent * members && countable >= MediumVotes ? Confidence.Medium
        : Confidence.Low;
}
