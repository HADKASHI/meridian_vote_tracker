using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Positions;

public abstract record PositionSaveResult
{
    public sealed record Saved(PositionDto Position) : PositionSaveResult;
    public sealed record Stale(PositionDto? Current) : PositionSaveResult;
    public sealed record VoteNotFound : PositionSaveResult;
}

/// <summary>
/// Setting a position: takes the vote's lock, inserts a revision and moves the pointer only
/// if it still points at the revision the editor saw. The revision id is the version.
/// </summary>
public static class PositionChanges
{
    public static async Task<PositionSaveResult> SaveAsync(MeridianDbContext db, long voteId, Stance stance, short weight,
        long? seenRevisionId, string setBy, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database
            .SqlQuery<long>($"SELECT v.id AS \"Value\" FROM vote v JOIN vote_current c ON c.vote_id = v.id WHERE v.id = {voteId} FOR UPDATE OF v")
            .ToListAsync(ct);
        if (locked.Count == 0) return new PositionSaveResult.VoteNotFound();

        var revision = new PositionRevision { VoteId = voteId, Stance = stance, Weight = weight, SetBy = setBy };
        db.PositionRevisions.Add(revision);
        await db.SaveChangesAsync(ct);

        var moved = await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE vote_current SET position_revision_id = {revision.Id}
             WHERE vote_id = {voteId} AND position_revision_id IS NOT DISTINCT FROM {seenRevisionId}
             """, ct);
        if (moved == 0)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return new PositionSaveResult.Stale(await CurrentAsync(db, voteId, ct));
        }

        await transaction.CommitAsync(ct);
        return new PositionSaveResult.Saved((await CurrentAsync(db, voteId, ct))!);
    }

    private static Task<PositionDto?> CurrentAsync(MeridianDbContext db, long voteId, CancellationToken ct) =>
        (from c in db.VoteCurrents
         join p in db.PositionRevisions on c.PositionRevisionId equals p.Id
         where c.VoteId == voteId
         select new PositionDto(p.Id, p.Stance, p.Weight, p.SetBy, p.RecordedAt))
        .AsNoTracking().SingleOrDefaultAsync(ct);
}
