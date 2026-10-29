using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Scoring;
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

    /// <summary>A page of snapshots, newest first; the vote count is null until the job is done.</summary>
    public static async Task<PageDto<SnapshotSummaryDto>> ListAsync(MeridianDbContext db, int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Snapshots.CountAsync(ct);
        var rows = await db.Snapshots.AsNoTracking().OrderByDescending(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new { s.Id, s.Name, s.CreatedBy, s.RecordedAt, Votes = db.SnapshotVotes.Count(v => v.SnapshotId == s.Id) })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var jobs = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.SnapshotId != null && ids.Contains(j.SnapshotId.Value)), ct))
            .ToDictionary(j => j.SnapshotId!.Value);
        var publications = (await PublicationQueries.ForSnapshotsAsync(db, ids, ct)).ToLookup(p => p.SnapshotId);

        return new PageDto<SnapshotSummaryDto>(rows.Select(r => new SnapshotSummaryDto(
                r.Id, r.Name, r.CreatedBy, r.RecordedAt, jobs[r.Id].State == JobState.Done ? r.Votes : null, jobs[r.Id],
                publications[r.Id].ToList()))
            .ToList(), total, page, pageSize);
    }

    /// <summary>One state's result and a line per snapshot vote, with the revisions the snapshot stored.</summary>
    public static async Task<SnapshotBreakdownDto?> BreakdownAsync(MeridianDbContext db, long snapshotId, string stateCode,
        CancellationToken ct)
    {
        var result = (await ResultsAsync(db, snapshotId, stateCode, ct)).SingleOrDefault();
        if (result is null) return null;

        var lines = await (
                from l in db.SnapshotLines
                where l.SnapshotId == snapshotId && l.StateCode == stateCode
                join sv in db.SnapshotVotes on new { l.SnapshotId, l.VoteId } equals new { sv.SnapshotId, sv.VoteId }
                join v in db.Votes on l.VoteId equals v.Id
                join d in db.VoteDetailsRevisions on sv.DetailsRevisionId equals d.Id
                join p in db.PositionRevisions on sv.PositionRevisionId equals p.Id
                join c in db.CountryVoteRevisions on l.CountryVoteRevisionId equals c.Id into ballots
                from c in ballots.DefaultIfEmpty()
                orderby d.VoteDate, v.AssemblyVoteId
                select new SnapshotLineDto(new VoteRefDto(v.Id, v.AssemblyVoteId), d.VoteDate, d.Title, p.Stance, p.Weight,
                    c != null ? c.Ballot : null, l.Category, l.CountryVoteRevisionId))
            .AsNoTracking().ToListAsync(ct);
        return new SnapshotBreakdownDto(result, lines);
    }

    /// <summary>
    /// A and B side by side from their stored results; nothing is recalculated. Score change is B minus A;
    /// rank change is A's rank minus B's, so a positive number means the state moved up. A state n/a in a
    /// snapshot, or missing from it, has no score or rank there and no movement.
    /// </summary>
    public static async Task<SnapshotComparisonDto> CompareAsync(MeridianDbContext db, SnapshotRefDto a, SnapshotRefDto b,
        CancellationToken ct)
    {
        var stored = await db.SnapshotResults.AsNoTracking()
            .Where(r => r.SnapshotId == a.Id || r.SnapshotId == b.Id)
            .Select(r => new { r.SnapshotId, r.StateCode, r.MatchedWeight, r.CountedWeight, r.Rank })
            .ToListAsync(ct);
        var names = await db.MemberStates.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, ct);

        StateResult? Find(long snapshotId, string code) => stored
            .Where(r => r.SnapshotId == snapshotId && r.StateCode == code)
            .Select(r => new StateResult(r.StateCode, r.MatchedWeight, r.CountedWeight, 0, r.Rank))
            .SingleOrDefault();

        var states = stored.Select(r => r.StateCode).Distinct().Select(code =>
        {
            var (resultA, resultB) = (Find(a.Id, code), Find(b.Id, code));
            var (scoreA, scoreB) = (resultA?.Score, resultB?.Score);
            var (rankA, rankB) = (resultA?.Rank, resultB?.Rank);
            return new StateMovementDto(code, names[code], scoreA, rankA, scoreB, rankB,
                scoreA is { } sa && scoreB is { } sb ? sb - sa : null,
                rankA is { } ra && rankB is { } rb ? ra - rb : null);
        });
        return new SnapshotComparisonDto(a, b, states
            .OrderBy(s => s.RankB is null).ThenBy(s => s.RankB).ThenBy(s => s.StateCode, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>The snapshot's name and time, or null when there is none.</summary>
    public static Task<SnapshotRefDto?> RefAsync(MeridianDbContext db, long snapshotId, CancellationToken ct) =>
        db.Snapshots.AsNoTracking().Where(s => s.Id == snapshotId)
            .Select(s => new SnapshotRefDto(s.Id, s.Name, s.RecordedAt)).SingleOrDefaultAsync(ct);

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
                new StateResult(r.StateCode, r.MatchedWeight, r.CountedWeight, r.Countable, r.Rank).Score, r.Rank))
            .ToList();
    }
}

/// <summary>Reads publications with the snapshot they publish and what superseded them.</summary>
internal static class PublicationQueries
{
    public static async Task<List<PublicationDto>> ForSnapshotsAsync(MeridianDbContext db, IReadOnlyCollection<long> snapshotIds,
        CancellationToken ct) =>
        await Load(db, db.Publications.Where(p => snapshotIds.Contains(p.SnapshotId))).ToListAsync(ct);

    /// <summary>The quarter's published snapshot now: the end of its chain, the publication nothing supersedes.</summary>
    public static Task<PublicationDto?> CurrentAsync(MeridianDbContext db, string quarter, CancellationToken ct) =>
        Load(db, db.Publications.Where(p => p.Quarter == quarter
                && !db.Publications.Any(n => n.SupersedesPublicationId == p.Id)))
            .SingleOrDefaultAsync(ct);

    /// <summary>Each quarter's chain, first publication first, newest quarter first.</summary>
    public static async Task<List<QuarterPublicationsDto>> QuartersAsync(MeridianDbContext db, CancellationToken ct) =>
        (await Load(db, db.Publications).ToListAsync(ct))
            .GroupBy(p => p.Quarter)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .Select(g => new QuarterPublicationsDto(g.Key, g.Single(p => p.SupersededByPublicationId is null), g.ToList()))
            .ToList();

    public static IQueryable<PublicationDto> Load(MeridianDbContext db, IQueryable<Publication> publications) =>
        from p in publications
        join s in db.Snapshots on p.SnapshotId equals s.Id
        orderby p.Id
        select new PublicationDto(
            p.Id, p.SnapshotId, s.Name, p.Quarter, p.PublishedBy, p.RecordedAt, p.Reason, p.SupersedesPublicationId,
            db.Publications.Where(n => n.SupersedesPublicationId == p.Id).Select(n => (long?)n.Id).FirstOrDefault());
}
