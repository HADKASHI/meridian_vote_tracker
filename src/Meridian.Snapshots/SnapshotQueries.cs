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
            snapshot.ScoringRulesVersion, RuleSetDto.Stored(snapshot.ScoringRulesVersion), snapshot.CreatedBy, snapshot.RecordedAt, job, voteCount,
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
        var names = await db.MemberStates.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, ct);
        // Each side is read under the rule set it was scored under; a stored id no rule set has reads as the default's rules.
        var (rulesA, rulesB) = (RulesOf(a), RulesOf(b));
        var unit = rulesA.WeightUnit * rulesB.WeightUnit / Gcd(rulesA.WeightUnit, rulesB.WeightUnit);
        var (resultsA, resultsB) = (await StoredAsync(db, a.Id, ct), await StoredAsync(db, b.Id, ct));
        var reasons = ReasonsFor(await StoredLinesAsync(db, a.Id, ct), rulesA, await StoredLinesAsync(db, b.Id, ct), rulesB, unit);

        var states = resultsA.Keys.Union(resultsB.Keys).Select(code =>
        {
            var (resultA, resultB) = (resultsA.GetValueOrDefault(code), resultsB.GetValueOrDefault(code));
            var (scoreA, scoreB) = (resultA?.Score, resultB?.Score);
            var (rankA, rankB) = (resultA?.Rank, resultB?.Rank);
            return new StateMovementDto(code, names[code], scoreA, rankA, BasisOf(resultA, unit / rulesA.WeightUnit), scoreB, rankB,
                BasisOf(resultB, unit / rulesB.WeightUnit),
                scoreA is { } sa && scoreB is { } sb ? sb - sa : null,
                rankA is { } ra && rankB is { } rb ? ra - rb : null, reasons[code].ToList());
        });
        return new SnapshotComparisonDto(a, b, a.RuleSet.Id != b.RuleSet.Id, unit, states
            .OrderBy(s => s.RankB is null).ThenBy(s => s.RankB).ThenBy(s => s.StateCode, StringComparer.Ordinal)
            .ToList());
    }

    private static RuleSet RulesOf(SnapshotRefDto snapshot) => RuleSets.TryGet(snapshot.RuleSet.Id, out var rules) ? rules : RuleSets.Default;

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    private sealed record StoredLine(
        long VoteId, string AssemblyVoteId, DateOnly VoteDate, string Title, string StateCode, LineCategory Category, Ballot? Ballot,
        Stance Stance, short Weight)
    {
        /// <summary>Matched and counted weight this line adds under <paramref name="rules"/>, in points of 1 ÷ <paramref name="unit"/> of a weight.</summary>
        public (int Matched, int Counted) Points(RuleSet rules, int unit)
        {
            var (matched, counted) = rules.Points(Category, Weight);
            return (matched * (unit / rules.WeightUnit), counted * (unit / rules.WeightUnit));
        }

        public ReasonSideDto Side => new(Category, Ballot, Stance, Weight);
    }

    /// <summary>Every stored line of a snapshot with the position and the ballot it used.</summary>
    private static async Task<List<StoredLine>> StoredLinesAsync(MeridianDbContext db, long snapshotId, CancellationToken ct) =>
        await (
                from l in db.SnapshotLines
                where l.SnapshotId == snapshotId
                join sv in db.SnapshotVotes on new { l.SnapshotId, l.VoteId } equals new { sv.SnapshotId, sv.VoteId }
                join v in db.Votes on l.VoteId equals v.Id
                join d in db.VoteDetailsRevisions on sv.DetailsRevisionId equals d.Id
                join p in db.PositionRevisions on sv.PositionRevisionId equals p.Id
                join c in db.CountryVoteRevisions on l.CountryVoteRevisionId equals c.Id into ballots
                from c in ballots.DefaultIfEmpty()
                select new StoredLine(v.Id, v.AssemblyVoteId, d.VoteDate, d.Title, l.StateCode, l.Category, c != null ? c.Ballot : null,
                    p.Stance, p.Weight))
            .AsNoTracking().ToListAsync(ct);

    /// <summary>
    /// Per state, the votes whose matched or counted weight differs between A and B, by vote date then assembly
    /// vote id like the breakdown. A vote that only moved between abstain, absent, non-member and no data
    /// changes no score and is not a reason.
    /// </summary>
    private static ILookup<string, MovementReasonDto> ReasonsFor(
        List<StoredLine> linesA, RuleSet rulesA, List<StoredLine> linesB, RuleSet rulesB, int unit)
    {
        var inA = linesA.ToDictionary(l => (l.VoteId, l.StateCode));
        var inB = linesB.ToDictionary(l => (l.VoteId, l.StateCode));
        return inA.Keys.Union(inB.Keys)
            .Select(key => (key.StateCode, Date: (inB.GetValueOrDefault(key) ?? inA[key]).VoteDate,
                Reason: ReasonFor(inA.GetValueOrDefault(key), rulesA, inB.GetValueOrDefault(key), rulesB, unit)))
            .Where(r => r.Reason is not null)
            .OrderBy(r => r.Date).ThenBy(r => r.Reason!.Vote.AssemblyVoteId, StringComparer.Ordinal)
            .ToLookup(r => r.StateCode, r => r.Reason!);
    }

    /// <summary>Why a state's weights differ on one vote, or null when A and B give it the same matched and counted weight.</summary>
    private static MovementReasonDto? ReasonFor(StoredLine? a, RuleSet rulesA, StoredLine? b, RuleSet rulesB, int unit)
    {
        var (pointsA, pointsB) = (a?.Points(rulesA, unit) ?? (0, 0), b?.Points(rulesB, unit) ?? (0, 0));
        var matchedChange = pointsB.Matched - pointsA.Matched;
        var countedChange = pointsB.Counted - pointsA.Counted;
        if (matchedChange == 0 && countedChange == 0) return null;

        var line = (b ?? a)!;
        var kind = a is null ? ReasonKind.OnlyInB : b is null ? ReasonKind.OnlyInA : ReasonKind.Changed;
        return new MovementReasonDto(new VoteRefDto(line.VoteId, line.AssemblyVoteId), line.Title, kind,
            a is not null && b is not null ? ChangesBetween(a, b) is { Count: > 0 } changes ? changes : [ReasonChange.Rules] : [], a?.Side, b?.Side, matchedChange, countedChange);
    }

    private static List<ReasonChange> ChangesBetween(StoredLine a, StoredLine b)
    {
        var changes = new List<ReasonChange>();
        if (a.Category != b.Category) changes.Add(ReasonChange.Category);
        if (a.Ballot != b.Ballot) changes.Add(ReasonChange.Ballot);
        if (a.Stance != b.Stance) changes.Add(ReasonChange.Stance);
        if (a.Weight != b.Weight) changes.Add(ReasonChange.Weight);
        return changes;
    }

    /// <summary>True when every one of the snapshots has finished calculating.</summary>
    public static async Task<bool> AllCalculatedAsync(MeridianDbContext db, IReadOnlyCollection<long> snapshotIds, CancellationToken ct) =>
        await db.Jobs.CountAsync(j => j.SnapshotId != null && snapshotIds.Contains(j.SnapshotId.Value) && j.State == JobState.Done, ct)
            == snapshotIds.Count;

    /// <summary>The basis in points of the comparison's unit; <paramref name="scale"/> is that unit ÷ the snapshot's own.</summary>
    private static ScoreBasisDto? BasisOf(StateResult? result, int scale) =>
        result is null ? null : new ScoreBasisDto(result.CountableVotes, result.MatchedWeight * scale, result.CountedWeight * scale);

    /// <summary>A snapshot's stored results by state code, each with its countable votes counted from the stored lines.</summary>
    private static async Task<Dictionary<string, StateResult>> StoredAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var countable = await db.SnapshotLines.AsNoTracking()
            .Where(l => l.SnapshotId == snapshotId && (l.Category == LineCategory.Match || l.Category == LineCategory.Mismatch))
            .GroupBy(l => l.StateCode).Select(g => new { StateCode = g.Key, Votes = g.Count() })
            .ToDictionaryAsync(g => g.StateCode, g => g.Votes, ct);
        var results = await db.SnapshotResults.AsNoTracking().Where(r => r.SnapshotId == snapshotId).ToListAsync(ct);
        return results.ToDictionary(r => r.StateCode, r => new StateResult(r.StateCode, r.MatchedWeight, r.CountedWeight,
            countable.GetValueOrDefault(r.StateCode), r.Rank));
    }

    /// <summary>The snapshot's name and time, or null when there is none.</summary>
    public static async Task<SnapshotRefDto?> RefAsync(MeridianDbContext db, long snapshotId, CancellationToken ct)
    {
        var snapshot = await db.Snapshots.AsNoTracking().Where(s => s.Id == snapshotId)
            .Select(s => new { s.Id, s.Name, s.RecordedAt, s.ScoringRulesVersion }).SingleOrDefaultAsync(ct);
        return snapshot is null ? null
            : new SnapshotRefDto(snapshot.Id, snapshot.Name, snapshot.RecordedAt, RuleSetDto.Stored(snapshot.ScoringRulesVersion));
    }

    /// <summary>Ranked states by rank then code, then the n/a states by code; or just one state.</summary>
    public static async Task<List<SnapshotResultDto>> ResultsAsync(MeridianDbContext db, long snapshotId, string? stateCode,
        CancellationToken ct)
    {
        var results = db.SnapshotResults.Where(r => r.SnapshotId == snapshotId);
        var lines = db.SnapshotLines.Where(l => l.SnapshotId == snapshotId);
        if (stateCode is not null)
        {
            results = results.Where(r => r.StateCode == stateCode);
            lines = lines.Where(l => l.StateCode == stateCode);
        }

        var rows = await (
                from r in results
                join s in db.MemberStates on r.StateCode equals s.Code
                select new { r.StateCode, s.Name, r.MatchedWeight, r.CountedWeight, r.Rank })
            .AsNoTracking().ToListAsync(ct);
        var counts = (await lines.GroupBy(l => new { l.StateCode, l.Category })
                .Select(g => new { g.Key.StateCode, g.Key.Category, Lines = g.Count() }).AsNoTracking().ToListAsync(ct))
            .ToLookup(c => c.StateCode);

        return rows
            .OrderBy(r => r.Rank is null).ThenBy(r => r.Rank).ThenBy(r => r.StateCode, StringComparer.Ordinal)
            .Select(r =>
            {
                int Of(LineCategory category) => counts[r.StateCode].Where(c => c.Category == category).Sum(c => c.Lines);
                var evidence = EvidenceMetrics.From(new LineCounts(Of(LineCategory.Match), Of(LineCategory.Mismatch),
                    Of(LineCategory.Abstain), Of(LineCategory.Absent), Of(LineCategory.NoData)));
                var countable = Of(LineCategory.Match) + Of(LineCategory.Mismatch);
                return new SnapshotResultDto(r.StateCode, r.Name, r.MatchedWeight, r.CountedWeight, countable,
                    new StateResult(r.StateCode, r.MatchedWeight, r.CountedWeight, countable, r.Rank).Score, r.Rank,
                    evidence.Completeness, evidence.AbstentionRate, evidence.MissingData, evidence.Confidence);
            })
            .ToList();
    }
}

/// <summary>Reads publications with the snapshot they publish and what superseded them.</summary>
internal static class PublicationQueries
{
    public static async Task<List<PublicationDto>> ForSnapshotsAsync(MeridianDbContext db, IReadOnlyCollection<long> snapshotIds,
        CancellationToken ct) =>
        ToDtos(await Load(db, db.Publications.Where(p => snapshotIds.Contains(p.SnapshotId))).ToListAsync(ct));

    /// <summary>The quarter's published snapshot now: the end of its chain, the publication nothing supersedes.</summary>
    public static async Task<PublicationDto?> CurrentAsync(MeridianDbContext db, string quarter, CancellationToken ct) =>
        ToDtos(await Load(db, db.Publications.Where(p => p.Quarter == quarter
                && !db.Publications.Any(n => n.SupersedesPublicationId == p.Id)))
            .ToListAsync(ct)).SingleOrDefault();

    /// <summary>Each quarter's chain, first publication first, newest quarter first.</summary>
    public static async Task<List<QuarterPublicationsDto>> QuartersAsync(MeridianDbContext db, CancellationToken ct) =>
        ToDtos(await Load(db, db.Publications).ToListAsync(ct))
            .GroupBy(p => p.Quarter)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .Select(g => new QuarterPublicationsDto(g.Key, g.Single(p => p.SupersededByPublicationId is null), g.ToList()))
            .ToList();

    public static async Task<PublicationDto> CreatedAsync(MeridianDbContext db, long publicationId, CancellationToken ct) =>
        ToDtos(await Load(db, db.Publications.Where(p => p.Id == publicationId)).ToListAsync(ct)).Single();

    /// <summary>A publication as stored, with the rules version of its snapshot, which the rule set names are read from.</summary>
    private sealed record Row(
        long Id, long SnapshotId, string SnapshotName, string Quarter, string PublishedBy, DateTimeOffset PublishedAt,
        string? Reason, long? SupersedesPublicationId, long? SupersededByPublicationId, string ScoringRulesVersion);

    private static List<PublicationDto> ToDtos(List<Row> rows) => rows.Select(r =>
    {
        var ruleSet = RuleSetDto.Stored(r.ScoringRulesVersion);
        return new PublicationDto(r.Id, r.SnapshotId, r.SnapshotName, r.Quarter, r.PublishedBy, r.PublishedAt, r.Reason,
            r.SupersedesPublicationId, r.SupersededByPublicationId, ruleSet.Id, ruleSet.Name);
    }).ToList();

    private static IQueryable<Row> Load(MeridianDbContext db, IQueryable<Publication> publications) =>
        from p in publications
        join s in db.Snapshots on p.SnapshotId equals s.Id
        orderby p.Id
        select new Row(
            p.Id, p.SnapshotId, s.Name, p.Quarter, p.PublishedBy, p.RecordedAt, p.Reason, p.SupersedesPublicationId,
            db.Publications.Where(n => n.SupersedesPublicationId == p.Id).Select(n => (long?)n.Id).FirstOrDefault(),
            s.ScoringRulesVersion);
}
