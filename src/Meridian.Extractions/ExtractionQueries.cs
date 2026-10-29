using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain.Sheets;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>The extraction reads. Issue counts come from the stored check; the job comes from the job table.</summary>
internal static class ExtractionQueries
{
    public static async Task<PageDto<ExtractionSummaryDto>> ListAsync(MeridianDbContext db, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await db.Extractions.CountAsync(ct);
        var extractions = await db.Extractions.AsNoTracking().OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = extractions.Select(x => x.Id).ToList();
        var jobs = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.ExtractionId != null && ids.Contains(j.ExtractionId.Value)), ct))
            .ToDictionary(j => j.ExtractionId!.Value);

        return new PageDto<ExtractionSummaryDto>(extractions.Select(x =>
        {
            var check = ExtractionJson.Read<SheetCheck>(x.CheckResult);
            return new ExtractionSummaryDto(x.Id, x.FileName, x.UploadedBy, x.RecordedAt, x.SizeBytes, x.Status, check?.VoteId, jobs[x.Id],
                check?.Issues.Count(i => i.Severity == SheetIssueSeverity.Blocking) ?? 0,
                check?.Issues.Count(i => i.Severity == SheetIssueSeverity.Warning) ?? 0);
        }).ToList(), total, page, pageSize);
    }

    public static async Task<ExtractionDto?> GetAsync(MeridianDbContext db, long id, CancellationToken ct)
    {
        var extraction = await db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (extraction is null) return null;
        var job = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.ExtractionId == id), ct)).Single();
        return ToDto(extraction, job);
    }

    private static ExtractionDto ToDto(Extraction x, JobDto job)
    {
        var sheet = ExtractionJson.Read<MappedSheet>(x.Sheet);
        var check = ExtractionJson.Read<SheetCheck>(x.CheckResult);
        return new ExtractionDto(x.Id, x.FileName, x.Sha256, x.SizeBytes, x.UploadedBy, x.RecordedAt, x.Status, job, x.Error,
            check?.VoteId, sheet?.Header, sheet?.Rows ?? [], check?.Counted, check?.Printed, check?.Issues ?? [], check?.CanApprove ?? false);
    }
}
