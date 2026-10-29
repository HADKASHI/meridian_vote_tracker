using Meridian.Domain.Scoring;

namespace Meridian.Domain.Tests.Scoring;

/// <summary>ADR 0007: completeness, abstention rate, missing data and the confidence band, from line counts alone.</summary>
public sealed class EvidenceMetricsTests
{
    private static Evidence Of(int match = 0, int mismatch = 0, int abstain = 0, int absent = 0, int noData = 0) =>
        EvidenceMetrics.From(new LineCounts(match, mismatch, abstain, absent, noData));

    [Fact]
    public void Metrics_are_shares_of_the_votes_the_state_was_a_member_for()
    {
        var evidence = Of(match: 6, mismatch: 2, abstain: 3, absent: 1, noData: 8);

        Assert.Equal(20, evidence.MemberVotes);
        Assert.Equal(0.4, evidence.Completeness);
        Assert.Equal(0.15, evidence.AbstentionRate);
        Assert.Equal(0.4, evidence.MissingData);
    }

    [Fact]
    public void A_state_that_was_never_a_member_has_no_metrics_and_no_band()
    {
        Assert.Equal(new Evidence(0, null, null, null, null), Of());
    }

    [Theory]
    [InlineData(10, 0, Confidence.High)]
    [InlineData(9, 1, Confidence.Medium)] // 9 countable of 10: 90% but only 9 votes
    [InlineData(18, 2, Confidence.High)] // 18 of 20 = 90% exactly
    [InlineData(17, 3, Confidence.Medium)] // 85% with 17 countable
    [InlineData(14, 6, Confidence.Medium)] // 70% exactly
    [InlineData(13, 7, Confidence.Low)] // 65%
    [InlineData(5, 0, Confidence.Medium)]
    [InlineData(4, 0, Confidence.Low)] // 100% but only 4 countable
    public void The_band_comes_from_completeness_and_the_number_of_countable_votes(int countable, int noData, Confidence band)
    {
        var evidence = Of(match: countable - countable / 3, mismatch: countable / 3, noData: noData);

        Assert.Equal(band, evidence.Confidence);
    }

    [Fact]
    public void Medium_needs_five_countable_votes_even_at_full_completeness_and_seventy_percent_at_least()
    {
        Assert.Equal(Confidence.Low, Of(match: 4).Confidence);
        Assert.Equal(Confidence.Medium, Of(match: 5).Confidence);
        Assert.Equal(Confidence.Low, Of(match: 6, noData: 3).Confidence); // 6 of 9 = 66.7%
        Assert.Equal(Confidence.Medium, Of(match: 7, noData: 3).Confidence); // 70%
    }

    [Fact]
    public void A_state_with_nothing_countable_has_metrics_but_no_band()
    {
        var evidence = Of(abstain: 4, absent: 3, noData: 3);

        Assert.Equal(10, evidence.MemberVotes);
        Assert.Equal(0.0, evidence.Completeness);
        Assert.Equal(0.4, evidence.AbstentionRate);
        Assert.Null(evidence.Confidence);
    }
}
