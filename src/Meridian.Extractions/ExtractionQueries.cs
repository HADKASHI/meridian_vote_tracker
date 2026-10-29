using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Platform.Contracts;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>The extraction reads. Issue counts come from the stored check; the job comes from the job table.</summary>
internal static class ExtractionQueries
{
    /// <param name="status">Narrows to one status as the analyst sees it, so <c>failed</c> includes a job that failed.</param>
    public static async Task<PageDto<ExtractionSummaryDto>> ListAsync(
        MeridianDbContext db, ExtractionStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var matching = from x in db.Extractions
                       join j in db.Jobs on x.Id equals j.ExtractionId
                       where status == null
                           || (status == ExtractionStatus.Failed
                               ? x.Status == ExtractionStatus.Failed || (x.Status == ExtractionStatus.Extracting && j.State == JobState.Failed)
                               : status == ExtractionStatus.Extracting
                                   ? x.Status == ExtractionStatus.Extracting && j.State != JobState.Failed
                                   : x.Status == status)
                       select x;
        var total = await matching.CountAsync(ct);
        var extractions = await matching.AsNoTracking().OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = extractions.Select(x => x.Id).ToList();
        var jobs = (await JobQueries.LoadAsync(db, db.Jobs.Where(j => j.ExtractionId != null && ids.Contains(j.ExtractionId.Value)), ct))
            .ToDictionary(j => j.ExtractionId!.Value);

        return new PageDto<ExtractionSummaryDto>(extractions.Select(x =>
        {
            var check = ExtractionJson.Read<SheetCheck>(x.CheckResult);
            return new ExtractionSummaryDto(x.Id, x.FileName, x.UploadedBy, x.RecordedAt, x.SizeBytes,
                StatusOf(x, jobs[x.Id]).Status, check?.VoteId, jobs[x.Id],
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
        var (status, error) = StatusOf(x, job);
        return new ExtractionDto(x.Id, x.FileName, x.Sha256, x.SizeBytes, x.UploadedBy, x.RecordedAt, status, job, error,
            check?.VoteId, sheet?.Header, sheet?.Rows ?? [], check?.Counted, check?.Printed, check?.Issues ?? [], check?.CanApprove ?? false,
            ExtractionJson.Read<List<ExtractionEditDto>>(x.Edits) ?? [], DecisionOf(x), x.ImportId);
    }

    public static ExtractionDecisionDto? DecisionOf(Extraction x) =>
        x.DecidedBy is { } by ? new ExtractionDecisionDto(x.Status, by, x.DecidedAt!.Value, x.DecisionComment) : null;

    /// <summary>
    /// What the analyst sees. A sheet the handler failed is stored as failed; one whose job failed (a timeout,
    /// a worker that stopped, retries used up) is still stored as extracting, so its job's failure is its own.
    /// </summary>
    public static (ExtractionStatus Status, string? Error) StatusOf(Extraction x, JobDto job) =>
        x.Status == ExtractionStatus.Extracting && job.State == JobState.Failed
            ? (ExtractionStatus.Failed, job.LastError ?? "Extraction failed.")
            : (x.Status, x.Error);
}
