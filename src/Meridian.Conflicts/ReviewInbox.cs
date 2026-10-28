using Meridian.Data;
using Meridian.Data.Entities;
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

    /// <summary>The decision pane: both values with their origins, the decision if any, and losing attempts.</summary>
    public static async Task<ConflictDto?> GetAsync(MeridianDbContext db, long conflictId, CancellationToken ct)
    {
        var conflict = await db.Conflicts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conflictId, ct);
        if (conflict is null) return null;
        var vote = await db.Votes.AsNoTracking().SingleAsync(v => v.Id == conflict.VoteId, ct);
        var decision = await db.ConflictDecisions.AsNoTracking().SingleOrDefaultAsync(d => d.ConflictId == conflictId, ct);

        var current = await CurrentSideAsync(db, conflict, ct);
        // The incoming value's evidence is the first file row that brought it.
        var firstRowId = await db.ImportRowConflicts.Where(l => l.ConflictId == conflictId)
            .MinAsync(l => (long?)l.ImportRowId, ct);
        var incoming = new ConflictSideDto(ConflictValues.Incoming(conflict),
            decision is { Decision: Decision.Accept } ? await CreatedRevisionIdAsync(db, conflict, decision.Id, ct) : null,
            await Origins.ImportRowAsync(db, firstRowId!.Value, ct));

        var notApplied = await db.DecisionAttemptsNotApplied.AsNoTracking()
            .Where(a => a.ConflictId == conflictId).OrderBy(a => a.Id)
            .Select(a => new NotAppliedAttemptDto(a.Id, a.AttemptedBy, a.Attempted, a.RecordedAt))
            .ToListAsync(ct);

        return new ConflictDto(
            conflict.Id, new VoteRefDto(vote.Id, vote.AssemblyVoteId), conflict.Subject, conflict.StateCode,
            current, incoming,
            conflict.ComparedCountryVoteRevisionId ?? conflict.ComparedDetailsRevisionId!.Value,
            decision is null ? null : await DecisionAsync(db, conflict, decision, ct),
            notApplied);
    }

    /// <summary>A decision with from (the value it replaced) → to (the incoming value).</summary>
    public static async Task<ConflictDecisionDto> DecisionAsync(MeridianDbContext db, Conflict conflict,
        ConflictDecision decision, CancellationToken ct)
    {
        var from = conflict.Subject == ConflictSubject.Ballot
            ? ConflictValues.Ballot(await db.CountryVoteRevisions.Where(r => r.Id == decision.ReplacedCountryVoteRevisionId)
                .Select(r => r.Ballot).SingleAsync(ct))
            : await db.VoteDetailsRevisions.Where(r => r.Id == decision.ReplacedDetailsRevisionId)
                .Select(r => ConflictValues.Details(r.Session, r.VoteDate, r.Title)).SingleAsync(ct);
        return new ConflictDecisionDto(decision.Id, conflict.Id, decision.Decision, decision.DecidedBy, decision.RecordedAt,
            decision.Comment, from, ConflictValues.Incoming(conflict),
            conflict.Subject == ConflictSubject.Details ? await MembershipRecalculation.ReadAsync(db, conflict, decision, ct) : null);
    }

    private static async Task<ConflictSideDto?> CurrentSideAsync(MeridianDbContext db, Conflict conflict, CancellationToken ct)
    {
        if (conflict.Subject == ConflictSubject.Ballot)
        {
            var ballot = await db.CurrentCountryVotes.AsNoTracking()
                .SingleOrDefaultAsync(c => c.VoteId == conflict.VoteId && c.StateCode == conflict.StateCode, ct);
            return ballot is null ? null : new ConflictSideDto(ConflictValues.Ballot(ballot.Ballot), ballot.RevisionId,
                await Origins.OfAsync(db, ballot.OriginImportRowId, ballot.OriginDecisionId, ct));
        }
        var pointer = await db.VoteCurrents.AsNoTracking().SingleOrDefaultAsync(c => c.VoteId == conflict.VoteId, ct);
        if (pointer is null) return null;
        var details = await db.VoteDetailsRevisions.AsNoTracking().SingleAsync(r => r.Id == pointer.DetailsRevisionId, ct);
        return new ConflictSideDto(ConflictValues.Details(details.Session, details.VoteDate, details.Title), details.Id,
            await Origins.OfAsync(db, details.OriginImportRowId, details.OriginDecisionId, ct));
    }

    private static Task<long?> CreatedRevisionIdAsync(MeridianDbContext db, Conflict conflict, long decisionId,
        CancellationToken ct) =>
        conflict.Subject == ConflictSubject.Ballot
            ? db.CountryVoteRevisions.Where(r => r.OriginDecisionId == decisionId && r.StateCode == conflict.StateCode)
                .Select(r => (long?)r.Id).SingleOrDefaultAsync(ct)
            : db.VoteDetailsRevisions.Where(r => r.OriginDecisionId == decisionId)
                .Select(r => (long?)r.Id).SingleOrDefaultAsync(ct);
}
