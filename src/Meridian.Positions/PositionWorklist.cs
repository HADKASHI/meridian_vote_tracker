using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Positions;

/// <summary>The worklist: votes without a position first, then by vote date and assembly vote id.</summary>
public static class PositionWorklist
{
    public static async Task<PageDto<PositionWorklistItemDto>> PageAsync(MeridianDbContext db, bool noPosition,
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var votes = noPosition ? db.CurrentVotes.Where(v => v.PositionRevisionId == null) : db.CurrentVotes;
        var total = await votes.CountAsync(ct);
        var items = await (
                from v in votes
                join p in db.PositionRevisions on v.PositionRevisionId equals p.Id into positions
                from p in positions.DefaultIfEmpty()
                orderby v.PositionRevisionId != null, v.VoteDate, v.AssemblyVoteId
                select new PositionWorklistItemDto(
                    new VoteRefDto(v.VoteId, v.AssemblyVoteId), v.Session, v.VoteDate, v.Title,
                    p == null ? null : new PositionDto(p.Id, p.Stance, p.Weight, p.SetBy, p.RecordedAt)))
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking().ToListAsync(ct);
        return new PageDto<PositionWorklistItemDto>(items, total, page, pageSize);
    }
}
