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

        return new ImportDto(
            import.Id, import.FileName, import.Sha256, import.SizeBytes, import.UploadedBy, import.RecordedAt,
            job, parse is null ? null : ToDto(parse), counts.GetValueOrDefault(importId, NoRows), [], []);
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
