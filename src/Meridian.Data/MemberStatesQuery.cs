using Meridian.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Data;

public static class MemberStatesQuery
{
    /// <summary>Every member state with its periods: the one reading the import and a voting sheet both check codes against.</summary>
    public static async Task<MemberStates> LoadMemberStatesAsync(this MeridianDbContext db, CancellationToken cancellationToken)
    {
        var periods = await db.MembershipPeriods.AsNoTracking().ToListAsync(cancellationToken);
        return new MemberStates(periods.GroupBy(p => p.StateCode.Trim()).ToDictionary(
            g => g.Key, g => (IReadOnlyList<MembershipSpan>)g.Select(p => new MembershipSpan(p.MemberFrom, p.MemberTo)).ToList()));
    }
}
