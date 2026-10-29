using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>
/// PDF-06: a failed extraction goes back to extracting and its job is queued again. The job row is reused (one job
/// per extraction), reset as if new. Both updates are conditional, so two retries at once queue it once.
/// </summary>
internal static class ExtractionRetry
{
    public static async Task<Results<Accepted<ExtractionCreatedDto>, NotFound, ProblemHttpResult>> HandleAsync(
        long extractionId, MeridianDbContext db, CancellationToken ct)
    {
        var jobId = await db.Jobs.Where(j => j.ExtractionId == extractionId).Select(j => (long?)j.Id).SingleOrDefaultAsync(ct);
        if (jobId is null) return TypedResults.NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Failed by its handler, or still extracting with a failed job (a timeout, retries used up, a stopped worker).
        var requeued = await db.Extractions
            .Where(x => x.Id == extractionId && (x.Status == ExtractionStatus.Failed
                || (x.Status == ExtractionStatus.Extracting && db.Jobs.Any(j => j.ExtractionId == x.Id && j.State == JobState.Failed))))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ExtractionStatus.Extracting).SetProperty(x => x.Error, (string?)null), ct);
        if (requeued == 0)
            return ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.NotRetryable,
                "Not failed", "Only a failed extraction can be tried again.");

        await JobQueue.RequeueAsync(db.Jobs.Where(j => j.Id == jobId), ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Accepted($"/api/extractions/{extractionId}", new ExtractionCreatedDto(extractionId, jobId.Value, Existing: false));
    }
}
