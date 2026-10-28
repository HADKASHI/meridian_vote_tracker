using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Conflicts;

/// <summary>
/// Decides a conflict in one transaction under the vote's row lock, so a decision never
/// interleaves with an import or another decision on the same vote. The decision records
/// the value it replaced: the current value now, not the one the conflict was compared
/// against. An accept adds a revision whose origin is the decision and moves the pointer.
/// </summary>
internal static class DecideConflict
{
    public abstract record Outcome;
    public sealed record NotFound : Outcome;
    public sealed record Decided(ConflictDecisionDto Decision) : Outcome;

    public static async Task<Outcome> RunAsync(MeridianDbContext db, long conflictId, Decision decision, string userId,
        string? comment, CancellationToken ct)
    {
        var conflict = await db.Conflicts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conflictId, ct);
        if (conflict is null) return new NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockVoteAsync(db, conflict.VoteId, ct);

        var record = conflict.Subject == ConflictSubject.Ballot
            ? await DecideBallotAsync(db, conflict, decision, userId, comment, ct)
            : await DecideDetailsAsync(db, conflict, decision, userId, comment, ct);

        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return new Decided(await ReviewInbox.DecisionAsync(db, conflict, record, ct));
    }

    private static async Task<ConflictDecision> DecideBallotAsync(MeridianDbContext db, Conflict conflict, Decision decision,
        string userId, string? comment, CancellationToken ct)
    {
        var pointer = await db.CountryVoteCurrents
            .SingleAsync(c => c.VoteId == conflict.VoteId && c.StateCode == conflict.StateCode, ct);
        var record = await RecordAsync(db, new ConflictDecision
        {
            ConflictId = conflict.Id, Decision = decision, DecidedBy = userId, Comment = comment,
            ReplacedCountryVoteRevisionId = pointer.RevisionId,
        }, ct);
        if (decision == Decision.Accept)
        {
            var revision = new CountryVoteRevision
            {
                VoteId = conflict.VoteId, StateCode = conflict.StateCode!, Ballot = conflict.IncomingBallot!.Value,
                OriginDecisionId = record.Id,
            };
            db.CountryVoteRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            pointer.RevisionId = revision.Id;
            await db.SaveChangesAsync(ct);
        }
        return record;
    }

    private static async Task<ConflictDecision> DecideDetailsAsync(MeridianDbContext db, Conflict conflict, Decision decision,
        string userId, string? comment, CancellationToken ct)
    {
        var pointer = await db.VoteCurrents.SingleAsync(c => c.VoteId == conflict.VoteId, ct);
        var record = await RecordAsync(db, new ConflictDecision
        {
            ConflictId = conflict.Id, Decision = decision, DecidedBy = userId, Comment = comment,
            ReplacedDetailsRevisionId = pointer.DetailsRevisionId,
        }, ct);
        if (decision == Decision.Accept)
        {
            var revision = new VoteDetailsRevision
            {
                VoteId = conflict.VoteId, Session = conflict.IncomingSession!.Value,
                VoteDate = conflict.IncomingVoteDate!.Value, Title = conflict.IncomingTitle!, OriginDecisionId = record.Id,
            };
            db.VoteDetailsRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            pointer.DetailsRevisionId = revision.Id;
            await db.SaveChangesAsync(ct);
        }
        return record;
    }

    private static async Task<ConflictDecision> RecordAsync(MeridianDbContext db, ConflictDecision record, CancellationToken ct)
    {
        db.ConflictDecisions.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    /// <summary>The per-vote lock (ADR 0005): a row lock, which the history guard does not refuse.</summary>
    private static Task LockVoteAsync(MeridianDbContext db, long voteId, CancellationToken ct) =>
        db.Database.SqlQuery<long>($"SELECT id AS \"Value\" FROM vote WHERE id = {voteId} FOR UPDATE").SingleAsync(ct);
}
