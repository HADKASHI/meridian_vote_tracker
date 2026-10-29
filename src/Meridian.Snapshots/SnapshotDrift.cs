using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Snapshots;

/// <summary>
/// Extra (story 103): compares the revisions a snapshot stored with the current ones. A pure read: it
/// runs no job, reads no score or rank and leaves the stored snapshot as it is.
/// </summary>
internal static class SnapshotDrift
{
    /// <summary>How many votes of each kind are listed; the count is always the real one.</summary>
    private const int Listed = 20;

    public static async Task<SnapshotChangesDto> ChangesAsync(MeridianDbContext db, long snapshotId, CancellationToken ct) =>
        new(DateTimeOffset.UtcNow, await PositionsAsync(db, snapshotId, ct), await DetailsAsync(db, snapshotId, ct));

    /// <summary>Votes whose position now has a different stance or weight than the one the snapshot used.</summary>
    private static async Task<PositionChangesDto> PositionsAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var changed =
            from sv in db.SnapshotVotes
            where sv.SnapshotId == snapshotId
            join current in db.VoteCurrents on sv.VoteId equals current.VoteId
            join v in db.Votes on sv.VoteId equals v.Id
            join old in db.PositionRevisions on sv.PositionRevisionId equals old.Id
            join now in db.PositionRevisions on current.PositionRevisionId equals now.Id
            where now.Id != old.Id && (now.Stance != old.Stance || now.Weight != old.Weight)
            select new { v.Id, v.AssemblyVoteId, Old = old, New = now };

        var count = await changed.CountAsync(ct);
        var rows = await changed.OrderBy(c => c.AssemblyVoteId).Take(Listed).AsNoTracking().ToListAsync(ct);
        return new PositionChangesDto(count, rows.Select(c => new PositionChangeDto(
            new VoteRefDto(c.Id, c.AssemblyVoteId), new PositionValueDto(c.Old.Stance, c.Old.Weight),
            new PositionValueDto(c.New.Stance, c.New.Weight),
            new ChangeOriginDto(ChangeOriginKind.Position, c.New.SetBy, null, null), c.New.RecordedAt)).ToList());
    }

    /// <summary>Votes whose approved details now differ from the ones the snapshot used.</summary>
    private static async Task<DetailsChangesDto> DetailsAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var changed =
            from sv in db.SnapshotVotes
            where sv.SnapshotId == snapshotId
            join current in db.VoteCurrents on sv.VoteId equals current.VoteId
            join v in db.Votes on sv.VoteId equals v.Id
            join old in db.VoteDetailsRevisions on sv.DetailsRevisionId equals old.Id
            join now in db.VoteDetailsRevisions on current.DetailsRevisionId equals now.Id
            where now.Id != old.Id && (now.Session != old.Session || now.VoteDate != old.VoteDate || now.Title != old.Title)
            select new { v.Id, v.AssemblyVoteId, Old = old, New = now };

        var count = await changed.CountAsync(ct);
        var rows = await changed.OrderBy(c => c.AssemblyVoteId).Take(Listed).AsNoTracking().ToListAsync(ct);

        var rowIds = rows.Select(r => r.New.OriginImportRowId).OfType<long>().ToList();
        var uploaders = await (
                from r in db.ImportRows
                where rowIds.Contains(r.Id)
                join i in db.Imports on r.ImportId equals i.Id
                select new { RowId = r.Id, ImportId = i.Id, i.UploadedBy })
            .AsNoTracking().ToDictionaryAsync(r => r.RowId, ct);
        var decisionIds = rows.Select(r => r.New.OriginDecisionId).OfType<long>().ToList();
        var deciders = await db.ConflictDecisions.AsNoTracking().Where(d => decisionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.DecidedBy, ct);

        return new DetailsChangesDto(count, rows.Select(c => new DetailsChangeDto(
            new VoteRefDto(c.Id, c.AssemblyVoteId), new DetailsValueDto(c.Old.Session, c.Old.VoteDate, c.Old.Title),
            new DetailsValueDto(c.New.Session, c.New.VoteDate, c.New.Title),
            c.New.OriginDecisionId is { } decision
                ? new ChangeOriginDto(ChangeOriginKind.Decision, deciders[decision], null, decision)
                : new ChangeOriginDto(ChangeOriginKind.Import, uploaders[c.New.OriginImportRowId!.Value].UploadedBy,
                    uploaders[c.New.OriginImportRowId.Value].ImportId, null),
            c.New.RecordedAt)).ToList());
    }
}
