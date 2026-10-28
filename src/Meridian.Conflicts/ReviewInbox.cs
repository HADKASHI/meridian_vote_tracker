using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Conflicts;

/// <summary>
/// Reads the Review inbox. The current value is always read live through the pointers,
/// never the revision the conflict was compared against, so a conflict opened before
/// another decision shows today's approved value.
/// </summary>
internal static class ReviewInbox
{
    public static async Task<PageDto<ConflictListItemDto>> ListAsync(MeridianDbContext db, ConflictFilter state,
        long[]? voteIds, long? importId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var rows =
            from c in db.Conflicts
            join v in db.Votes on c.VoteId equals v.Id
            join d in db.ConflictDecisions on c.Id equals d.ConflictId into decisions
            from d in decisions.DefaultIfEmpty()
            join cv in db.CurrentCountryVotes on new { c.VoteId, StateCode = c.StateCode! } equals new { cv.VoteId, cv.StateCode }
                into ballots
            from cv in ballots.DefaultIfEmpty()
            join cd in db.CurrentVotes on c.VoteId equals cd.VoteId into details
            from cd in details.DefaultIfEmpty()
            select new { Conflict = c, v.AssemblyVoteId, Decision = d, CurrentBallot = cv, CurrentDetails = cd };

        rows = state switch
        {
            ConflictFilter.Pending => rows.Where(r => r.Decision == null),
            ConflictFilter.Decided => rows.Where(r => r.Decision != null),
            _ => rows,
        };
        if (voteIds is { Length: > 0 }) rows = rows.Where(r => voteIds.Contains(r.Conflict.VoteId));
        if (importId is not null) rows = rows.Where(r => r.Conflict.OpenedByImportId == importId);

        var total = await rows.CountAsync(ct);
        // A vote's conflicts sit together: its details first, then its ballots by state.
        var items = await rows
            .OrderBy(r => r.AssemblyVoteId)
            .ThenBy(r => r.Conflict.Subject == ConflictSubject.Details ? 0 : 1)
            .ThenBy(r => r.Conflict.StateCode)
            .ThenBy(r => r.Conflict.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        return new PageDto<ConflictListItemDto>(items.Select(r =>
        {
            var c = r.Conflict;
            var current = c.Subject == ConflictSubject.Ballot
                ? r.CurrentBallot is null ? null : ConflictValues.Ballot(r.CurrentBallot.Ballot)
                : r.CurrentDetails is null ? null
                : ConflictValues.Details(r.CurrentDetails.Session, r.CurrentDetails.VoteDate, r.CurrentDetails.Title);
            return new ConflictListItemDto(
                InboxItemKind.Conflict, c.Id, new VoteRefDto(c.VoteId, r.AssemblyVoteId), c.Subject, c.StateCode,
                current, ConflictValues.Incoming(c), c.OpenedByImportId, c.RecordedAt, r.Decision?.Decision);
        }).ToList(), total, page, pageSize);
    }
}
