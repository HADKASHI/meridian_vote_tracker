using Meridian.Data;
using Meridian.Data.Entities;
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

    public static async Task<SnapshotChangesDto> ChangesAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var json = await db.Snapshots.AsNoTracking().Where(s => s.Id == snapshotId).Select(s => s.Selection).SingleAsync(ct);
        var selected = SnapshotSelection.Resolve(db, SnapshotJson.ReadSelection(json));
        return new SnapshotChangesDto(DateTimeOffset.UtcNow, await PositionsAsync(db, snapshotId, ct),
            await CountryVotesAsync(db, snapshotId, ct), await DetailsAsync(db, snapshotId, ct),
            await NowSelectedAsync(db, snapshotId, selected, ct), await NoLongerSelectedAsync(db, snapshotId, selected, ct));
    }

    /// <summary>Votes the saved selection matches today that the snapshot does not hold.</summary>
    private static async Task<NowSelectedDto> NowSelectedAsync(MeridianDbContext db, long snapshotId,
        IQueryable<CurrentVote> selected, CancellationToken ct)
    {
        var added = selected.Where(v => !db.SnapshotVotes.Any(sv => sv.SnapshotId == snapshotId && sv.VoteId == v.VoteId));
        var count = await added.CountAsync(ct);
        var rows = await added.OrderBy(v => v.AssemblyVoteId).Take(Listed).ToListAsync(ct);
        return new NowSelectedDto(count, rows.Select(v => new NowSelectedVoteDto(
            new VoteRefDto(v.VoteId, v.AssemblyVoteId), new DetailsValueDto(v.Session, v.VoteDate, v.Title),
            v.Stance is { } stance ? new PositionValueDto(stance, v.Weight!.Value) : null)).ToList());
    }

    /// <summary>Votes the snapshot holds that the saved selection no longer matches, with the details that moved them out.</summary>
    private static async Task<DetailsChangesDto> NoLongerSelectedAsync(MeridianDbContext db, long snapshotId,
        IQueryable<CurrentVote> selected, CancellationToken ct)
    {
        var left =
            from sv in db.SnapshotVotes
            where sv.SnapshotId == snapshotId && !selected.Any(v => v.VoteId == sv.VoteId)
            join current in db.VoteCurrents on sv.VoteId equals current.VoteId
            join v in db.Votes on sv.VoteId equals v.Id
            join old in db.VoteDetailsRevisions on sv.DetailsRevisionId equals old.Id
            join now in db.VoteDetailsRevisions on current.DetailsRevisionId equals now.Id
            select new { v.Id, v.AssemblyVoteId, Old = old, New = now };
        var count = await left.CountAsync(ct);
        var rows = await left.OrderBy(c => c.AssemblyVoteId).Take(Listed).AsNoTracking().ToListAsync(ct);
        var origins = await OriginsAsync(db, rows.Select(r => r.New.OriginImportRowId), rows.Select(r => r.New.OriginDecisionId), ct);
        return new DetailsChangesDto(count, rows.Select(c => DetailsChange(c.Id, c.AssemblyVoteId, c.Old, c.New, origins)).ToList());
    }

    private static DetailsChangeDto DetailsChange(long voteId, string assemblyVoteId, VoteDetailsRevision old,
        VoteDetailsRevision now, Func<long?, long?, ChangeOriginDto> origins) =>
        new(new VoteRefDto(voteId, assemblyVoteId), new DetailsValueDto(old.Session, old.VoteDate, old.Title),
            new DetailsValueDto(now.Session, now.VoteDate, now.Title),
            origins(now.OriginImportRowId, now.OriginDecisionId), now.RecordedAt);

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

    /// <summary>
    /// States whose approved ballot in a held vote is no longer the revision the snapshot used,
    /// including a state it had no data for that now has one.
    /// </summary>
    private static async Task<CountryVoteChangesDto> CountryVotesAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var changed =
            from l in db.SnapshotLines
            where l.SnapshotId == snapshotId
            join now in db.CountryVoteRevisions on new { l.VoteId, l.StateCode } equals new { now.VoteId, now.StateCode }
            join pointer in db.CountryVoteCurrents on now.Id equals pointer.RevisionId
            where l.CountryVoteRevisionId == null || l.CountryVoteRevisionId != now.Id
            join v in db.Votes on l.VoteId equals v.Id
            join old in db.CountryVoteRevisions on l.CountryVoteRevisionId equals old.Id into olds
            from old in olds.DefaultIfEmpty()
            select new { v.Id, v.AssemblyVoteId, l.StateCode, Old = old, New = now };

        var count = await changed.CountAsync(ct);
        var rows = await changed.OrderBy(c => c.AssemblyVoteId).ThenBy(c => c.StateCode).Take(Listed).AsNoTracking().ToListAsync(ct);
        var origins = await OriginsAsync(db, rows.Select(r => r.New.OriginImportRowId), rows.Select(r => r.New.OriginDecisionId), ct);
        return new CountryVoteChangesDto(count, rows.Select(c => new CountryVoteChangeDto(
            new VoteRefDto(c.Id, c.AssemblyVoteId), c.StateCode, c.Old?.Ballot, c.New.Ballot,
            origins(c.New.OriginImportRowId, c.New.OriginDecisionId), c.New.RecordedAt)).ToList());
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

        var origins = await OriginsAsync(db, rows.Select(r => r.New.OriginImportRowId), rows.Select(r => r.New.OriginDecisionId), ct);

        return new DetailsChangesDto(count, rows.Select(c => DetailsChange(c.Id, c.AssemblyVoteId, c.Old, c.New, origins)).ToList());
    }

    /// <summary>Resolves a revision's origin (an import row's import and uploader, or a decision and who made it).</summary>
    private static async Task<Func<long?, long?, ChangeOriginDto>> OriginsAsync(MeridianDbContext db,
        IEnumerable<long?> importRowIds, IEnumerable<long?> decisionIds, CancellationToken ct)
    {
        var rowIds = importRowIds.OfType<long>().ToList();
        var uploaders = await (
                from r in db.ImportRows
                where rowIds.Contains(r.Id)
                join i in db.Imports on r.ImportId equals i.Id
                select new { RowId = r.Id, ImportId = i.Id, i.UploadedBy })
            .AsNoTracking().ToDictionaryAsync(r => r.RowId, ct);
        var ids = decisionIds.OfType<long>().ToList();
        var deciders = await db.ConflictDecisions.AsNoTracking().Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.DecidedBy, ct);
        return (rowId, decisionId) => decisionId is { } decision
            ? new ChangeOriginDto(ChangeOriginKind.Decision, deciders[decision], null, decision)
            : new ChangeOriginDto(ChangeOriginKind.Import, uploaders[rowId!.Value].UploadedBy, uploaders[rowId.Value].ImportId, null);
    }
}
