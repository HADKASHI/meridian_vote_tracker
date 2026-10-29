using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Votes;

/// <summary>Finds votes with approved details; a vote seen only in a contradiction has none and isn't listed.</summary>
public static class VoteSearch
{
    public sealed record Filter(string? Q, int? Session, DateOnly? From, DateOnly? To, bool NoPosition);

    public static async Task<PageDto<VoteSummaryDto>> SearchAsync(MeridianDbContext db, Filter filter, int page, int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var votes = db.CurrentVotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var pattern = $"%{EscapeLike(filter.Q.Trim())}%";
            votes = votes.Where(v => EF.Functions.ILike(v.AssemblyVoteId, pattern, @"\")
                || EF.Functions.ILike(v.Title, pattern, @"\"));
        }
        if (filter.Session is { } session) votes = votes.Where(v => v.Session == session);
        if (filter.From is { } from) votes = votes.Where(v => v.VoteDate >= from);
        if (filter.To is { } to) votes = votes.Where(v => v.VoteDate <= to);
        if (filter.NoPosition) votes = votes.Where(v => v.PositionRevisionId == null);

        var total = await votes.CountAsync(ct);
        // Ordered after the join, so the page order holds whatever plan the join gets.
        var items = await (
                from v in votes
                join p in db.PositionRevisions on v.PositionRevisionId equals p.Id into positions
                from p in positions.DefaultIfEmpty()
                orderby v.VoteDate descending, v.AssemblyVoteId
                select new VoteSummaryDto(v.VoteId, v.AssemblyVoteId, v.Session, v.VoteDate, v.Title,
                    p == null ? null : new PositionDto(p.Id, p.Stance, p.Weight, p.SetBy, p.RecordedAt)))
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PageDto<VoteSummaryDto>(items, total, page, pageSize);
    }

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
