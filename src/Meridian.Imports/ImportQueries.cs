using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Imports;

/// <summary>The import report's reads. Counts are always queried from the row records, never stored.</summary>
internal static class ImportQueries
{
    public static async Task<PageDto<ImportSummaryDto>> ListAsync(MeridianDbContext db, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await db.Imports.CountAsync(ct);
        var imports = await db.Imports.AsNoTracking().OrderByDescending(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = imports.Select(i => i.Id).ToList();
        var jobs = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.ImportId != null && ids.Contains(j.ImportId.Value)), ct))
            .ToDictionary(j => j.ImportId!.Value);
        var rejected = await db.ImportParses.Where(p => ids.Contains(p.ImportId) && p.RejectedReason != null)
            .ToDictionaryAsync(p => p.ImportId, p => p.RejectedReason, ct);
        var counts = await CountsAsync(db, db.ImportRows.Where(r => ids.Contains(r.ImportId)), r => r.ImportId, ct);

        return new PageDto<ImportSummaryDto>(imports.Select(i => new ImportSummaryDto(
                i.Id, i.FileName, i.UploadedBy, i.RecordedAt, i.SizeBytes, jobs[i.Id],
                rejected.GetValueOrDefault(i.Id), counts.GetValueOrDefault(i.Id, NoRows)))
            .ToList(), total, page, pageSize);
    }

    public static async Task<ImportDto?> GetAsync(MeridianDbContext db, long importId, CancellationToken ct)
    {
        var import = await db.Imports.AsNoTracking().SingleOrDefaultAsync(i => i.Id == importId, ct);
        if (import is null) return null;
        var job = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.ImportId == importId), ct)).Single();
        var parse = await db.ImportParses.AsNoTracking().SingleOrDefaultAsync(p => p.ImportId == importId, ct);
        var counts = await CountsAsync(db, db.ImportRows.Where(r => r.ImportId == importId), r => r.ImportId, ct);
        var warnings = await (
                from w in db.ImportWarnings
                where w.ImportId == importId
                join v in db.Votes on w.VoteId equals v.Id into votes
                from v in votes.DefaultIfEmpty()
                orderby w.Id
                select new { Warning = w, Vote = v == null ? null : new VoteRefDto(v.Id, v.AssemblyVoteId) })
            .AsNoTracking().ToListAsync(ct);

        var opened = await (
                from c in db.Conflicts
                where c.OpenedByImportId == importId
                join v in db.Votes on c.VoteId equals v.Id
                orderby c.Id
                select new
                {
                    c.Id, v.AssemblyVoteId, c.VoteId, c.Subject, c.StateCode,
                    Rows = db.ImportRowConflicts.Count(l => l.ConflictId == c.Id
                        && db.ImportRows.Any(r => r.Id == l.ImportRowId && r.ImportId == importId)),
                })
            .AsNoTracking().ToListAsync(ct);

        return new ImportDto(
            import.Id, import.FileName, import.Sha256, import.SizeBytes, import.UploadedBy, import.RecordedAt,
            job, parse is null ? null : ToDto(parse), counts.GetValueOrDefault(importId, NoRows),
            warnings.Select(w => ImportWarningDetail.ToDto(w.Warning, w.Vote)).ToList(),
            opened.Select(c => new ImportConflictDto(c.Id, new VoteRefDto(c.VoteId, c.AssemblyVoteId), c.Subject,
                c.StateCode?.Trim(), c.Rows)).ToList());
    }

    public static async Task<PageDto<ImportRowDto>?> RowsAsync(MeridianDbContext db, long importId,
        Outcome? outcome, long? voteId, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Imports.AnyAsync(i => i.Id == importId, ct)) return null;
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = db.ImportRows.Where(r => r.ImportId == importId);
        if (outcome is not null) rows = rows.Where(r => r.Outcome == outcome);
        if (voteId is not null) rows = rows.Where(r => r.VoteId == voteId);

        var total = await rows.CountAsync(ct);
        var items = await (
                from r in rows.OrderBy(r => r.LineNo).Skip((page - 1) * pageSize).Take(pageSize)
                join v in db.Votes on r.VoteId equals v.Id into votes
                from v in votes.DefaultIfEmpty()
                select new { Row = r, Vote = v == null ? null : new VoteRefDto(v.Id, v.AssemblyVoteId) })
            .AsNoTracking().ToListAsync(ct);
        var ids = items.Select(i => i.Row.Id).ToList();
        var conflicts = (await db.ImportRowConflicts.Where(l => ids.Contains(l.ImportRowId)).ToListAsync(ct))
            .ToLookup(l => l.ImportRowId, l => l.ConflictId);

        return new PageDto<ImportRowDto>(items.Select(i => new ImportRowDto(
                i.Row.Id, i.Row.LineNo, i.Row.RawText, i.Row.Outcome, i.Row.ReasonCode, i.Row.ReasonMessage,
                i.Row.Normalisations is null ? [] : JsonSerializer.Deserialize<List<string>>(i.Row.Normalisations)!,
                i.Vote, i.Row.StateCode, i.Row.CountryVoteRevisionId, i.Row.DetailsRevisionId,
                conflicts[i.Row.Id].Order().ToList()))
            .ToList(), total, page, pageSize);
    }

    /// <summary>Counts per vote, in the order the votes first appear in the file.</summary>
    public static async Task<IReadOnlyList<ImportVoteCountsDto>?> VoteCountsAsync(MeridianDbContext db, long importId, CancellationToken ct)
    {
        if (!await db.Imports.AnyAsync(i => i.Id == importId, ct)) return null;
        var rows = db.ImportRows.Where(r => r.ImportId == importId && r.VoteId != null);
        var counts = await CountsAsync(db, rows, r => r.VoteId!.Value, ct);
        var firstLines = await rows.GroupBy(r => r.VoteId!.Value)
            .Select(g => new { VoteId = g.Key, FirstLine = g.Min(r => r.LineNo) }).ToListAsync(ct);
        var ids = firstLines.Select(f => f.VoteId).ToList();
        var votes = await db.Votes.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.AssemblyVoteId, ct);
        return firstLines.OrderBy(f => f.FirstLine)
            .Select(f => new ImportVoteCountsDto(new VoteRefDto(f.VoteId, votes[f.VoteId]), counts[f.VoteId]))
            .ToList();
    }

    public static readonly OutcomeCountsDto NoRows = new(0, 0, 0, 0, 0, 0);

    /// <summary>Counts per outcome of <paramref name="rows"/>, grouped by <paramref name="key"/>.</summary>
    public static async Task<Dictionary<TKey, OutcomeCountsDto>> CountsAsync<TKey>(MeridianDbContext db,
        IQueryable<ImportRow> rows, System.Linq.Expressions.Expression<Func<ImportRow, TKey>> key, CancellationToken ct)
        where TKey : notnull
    {
        var grouped = await rows.GroupBy(key).Select(g => new
            {
                g.Key,
                New = g.Count(r => r.Outcome == Outcome.New),
                Unchanged = g.Count(r => r.Outcome == Outcome.Unchanged),
                Duplicate = g.Count(r => r.Outcome == Outcome.Duplicate),
                Invalid = g.Count(r => r.Outcome == Outcome.Invalid),
                Contradiction = g.Count(r => r.Outcome == Outcome.Contradiction),
                Conflict = g.Count(r => r.Outcome == Outcome.Conflict),
            })
            .ToListAsync(ct);
        return grouped.ToDictionary(g => g.Key,
            g => new OutcomeCountsDto(g.New, g.Unchanged, g.Duplicate, g.Invalid, g.Contradiction, g.Conflict));
    }

    private static ImportParseDto ToDto(ImportParse parse) => new(
        JsonSerializer.Deserialize<List<string>>(parse.Header)!, parse.HasBom,
        SnakeCaseEnumConverter<LineEndings>.FromName(parse.LineEndings), parse.DataRows,
        parse.MaxPublishedAt, parse.RejectedReason);
}
