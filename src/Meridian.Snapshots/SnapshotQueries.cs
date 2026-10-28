using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Snapshots;

/// <summary>Reads what a snapshot stored. Nothing is recalculated when a snapshot is opened.</summary>
internal static class SnapshotQueries
{
    public static async Task<SnapshotDto?> GetAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var snapshot = await db.Snapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == snapshotId, ct);
        if (snapshot is null) return null;

        var job = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.SnapshotId == snapshotId), ct)).Single();
        var voteCount = await db.SnapshotVotes.CountAsync(v => v.SnapshotId == snapshotId, ct);
        var warnings = await (
                from w in db.SnapshotWarnings
                where w.SnapshotId == snapshotId
                join v in db.Votes on w.VoteId equals v.Id into votes
                from v in votes.DefaultIfEmpty()
                orderby w.Id
                select new { w.Kind, w.Detail, VoteId = (long?)v.Id, AssemblyVoteId = v != null ? v.AssemblyVoteId : null })
            .AsNoTracking().ToListAsync(ct);

        return new SnapshotDto(
            snapshot.Id, snapshot.Name, snapshot.Note, SnapshotJson.ReadSelection(snapshot.Selection),
            snapshot.ScoringRulesVersion, snapshot.CreatedBy, snapshot.RecordedAt, job, voteCount,
            await ResultsAsync(db, snapshotId, stateCode: null, ct),
            warnings.Select(w => SnapshotJson.ReadWarning(SnakeCaseEnumConverter<SnapshotWarningKind>.FromName(w.Kind),
                w.VoteId is { } id ? new VoteRefDto(id, w.AssemblyVoteId!) : null, w.Detail)).ToList(),
            await PublicationQueries.ForSnapshotsAsync(db, [snapshotId], ct));
    }

    /// <summary>Ranked states by rank then code, then the n/a states by code; or just one state.</summary>
    public static async Task<List<SnapshotResultDto>> ResultsAsync(MeridianDbContext db, long snapshotId, string? stateCode,
        CancellationToken ct)
    {
        var results = db.SnapshotResults.Where(r => r.SnapshotId == snapshotId);
        if (stateCode is not null) results = results.Where(r => r.StateCode == stateCode);
        var rows = await (
                from r in results
                join s in db.MemberStates on r.StateCode equals s.Code
                select new
                {
                    r.StateCode, s.Name, r.MatchedWeight, r.CountedWeight, r.Rank,
                    Countable = db.SnapshotLines.Count(l => l.SnapshotId == snapshotId && l.StateCode == r.StateCode
                        && (l.Category == LineCategory.Match || l.Category == LineCategory.Mismatch)),
                })
            .AsNoTracking().ToListAsync(ct);

        return rows
            .OrderBy(r => r.Rank is null).ThenBy(r => r.Rank).ThenBy(r => r.StateCode, StringComparer.Ordinal)
            .Select(r => new SnapshotResultDto(r.StateCode, r.Name, r.MatchedWeight, r.CountedWeight, r.Countable,
                r.CountedWeight == 0 ? null : 100.0 * r.MatchedWeight / r.CountedWeight, r.Rank))
            .ToList();
    }
}

/// <summary>Reads publications with the snapshot they publish and what superseded them.</summary>
internal static class PublicationQueries
{
    public static async Task<List<PublicationDto>> ForSnapshotsAsync(MeridianDbContext db, IReadOnlyCollection<long> snapshotIds,
        CancellationToken ct) =>
        await Load(db, db.Publications.Where(p => snapshotIds.Contains(p.SnapshotId))).ToListAsync(ct);

    public static IQueryable<PublicationDto> Load(MeridianDbContext db, IQueryable<Publication> publications) =>
        from p in publications
        join s in db.Snapshots on p.SnapshotId equals s.Id
        orderby p.Id
        select new PublicationDto(
            p.Id, p.SnapshotId, s.Name, p.Quarter, p.PublishedBy, p.RecordedAt, p.Reason, p.SupersedesPublicationId,
            db.Publications.Where(n => n.SupersedesPublicationId == p.Id).Select(n => (long?)n.Id).FirstOrDefault());
}
