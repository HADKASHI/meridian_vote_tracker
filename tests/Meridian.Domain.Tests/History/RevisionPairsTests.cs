using Meridian.Domain.History;

namespace Meridian.Domain.Tests.History;

/// <summary>Map ticket 04 rule 7: "previous" is the earlier revision of the same subject, in history order.</summary>
public sealed class RevisionPairsTests
{
    [Fact]
    public void Each_revision_is_paired_with_the_one_before_it_and_the_first_has_none()
    {
        var previous = RevisionPairs.PreviousOf([(10L, "vote 1"), (25L, "vote 1"), (40L, "vote 1")]);

        Assert.Equal(new Dictionary<long, long?> { [10] = null, [25] = 10, [40] = 25 }, previous);
    }

    [Fact]
    public void Pairs_never_cross_subjects()
    {
        var previous = RevisionPairs.PreviousOf([(11L, ("vote 1", "FRA")), (12L, ("vote 1", "BRA")), (30L, ("vote 1", "BRA")),
            (31L, ("vote 2", "FRA")), (35L, ("vote 1", "FRA"))]);

        Assert.Equal(new Dictionary<long, long?> { [11] = null, [12] = null, [30] = 12, [31] = null, [35] = 11 }, previous);
    }

    [Fact]
    public void History_order_is_the_id_not_the_order_given()
    {
        var previous = RevisionPairs.PreviousOf([(40L, 'p'), (10L, 'p'), (25L, 'p')]);

        Assert.Equal(new Dictionary<long, long?> { [10] = null, [25] = 10, [40] = 25 }, previous);
    }

    [Fact]
    public void No_revisions_means_no_pairs()
    {
        Assert.Empty(RevisionPairs.PreviousOf(Array.Empty<(long, int)>()));
    }
}
