using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Platform.Jobs;

/// <summary>What the header polls: running and queued jobs, jobs finished in the last minute, pending conflicts.</summary>
public sealed record StatusDto(
    IReadOnlyList<JobDto> Running, IReadOnlyList<JobDto> Queued, IReadOnlyList<JobDto> RecentlyFinished, int PendingConflicts);

public static class JobsEndpoints
{
    private static readonly TimeSpan RecentlyFinished = TimeSpan.FromMinutes(1);

    public static IEndpointRouteBuilder MapJobs(this IEndpointRouteBuilder api)
    {
        api.MapGet("/status", async Task<Ok<StatusDto>> (MeridianDbContext db, CancellationToken ct) =>
            {
                var since = DateTimeOffset.UtcNow - RecentlyFinished;
                var jobs = await JobQueries.LoadAsync(db, db.Jobs.Where(j =>
                    j.State == JobState.Running || j.State == JobState.Queued || j.FinishedAt > since), ct);
                return TypedResults.Ok(new StatusDto(
                    jobs.Where(j => j.State == JobState.Running).ToList(),
                    jobs.Where(j => j.State == JobState.Queued).ToList(),
                    jobs.Where(j => j.State is JobState.Done or JobState.Failed).OrderByDescending(j => j.FinishedAt).ToList(),
                    await db.PendingConflicts.CountAsync(ct)));
            })
            .WithTags("Jobs")
            .WithName("getStatus")
            .WithSummary("Polled by the header: the jobs indicator and the Review badge.")
            .ForAnalysts();

        api.MapGet("/jobs", async Task<Ok<PageDto<JobDto>>> (MeridianDbContext db, CancellationToken ct,
                JobState? state = null, int page = 1, int pageSize = 50) =>
            {
                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 200);
                var filtered = state is null ? db.Jobs : db.Jobs.Where(j => j.State == state);
                var total = await filtered.CountAsync(ct);
                var items = await JobQueries.LoadAsync(db,
                    filtered.OrderByDescending(j => j.Id).Skip((page - 1) * pageSize).Take(pageSize), ct);
                return TypedResults.Ok(new PageDto<JobDto>(items, total, page, pageSize));
            })
            .WithTags("Jobs")
            .WithName("listJobs")
            .WithSummary("Every job, newest first, with place in queue, progress and failure reason.")
            .ForAnalysts();

        return api;
    }
}

/// <summary>Reads jobs as <see cref="JobDto"/>; feature modules use it for their import or snapshot's job.</summary>
public static class JobQueries
{
    public static async Task<List<JobDto>> LoadAsync(MeridianDbContext db, IQueryable<Job> jobs, CancellationToken ct)
    {
        var rows = await (
                from j in jobs
                join i in db.Imports on j.ImportId equals i.Id into imports
                from i in imports.DefaultIfEmpty()
                join s in db.Snapshots on j.SnapshotId equals s.Id into snapshots
                from s in snapshots.DefaultIfEmpty()
                join x in db.Extractions on j.ExtractionId equals x.Id into extractions
                from x in extractions.DefaultIfEmpty()
                select new { Job = j, StartedBy = i != null ? i.UploadedBy : s != null ? s.CreatedBy : x!.UploadedBy })
            .AsNoTracking().ToListAsync(ct);

        // Place in queue counts the queued jobs of the same kind ahead of it.
        var queued = rows.Any(r => r.Job.State == JobState.Queued)
            ? await db.Jobs.Where(j => j.State == JobState.Queued).OrderBy(j => j.Id)
                .Select(j => new { j.Id, j.Kind }).ToListAsync(ct)
            : [];
        var places = queued.GroupBy(j => j.Kind)
            .SelectMany(g => g.Select((j, index) => (j.Id, Place: index + 1)))
            .ToDictionary(p => p.Id, p => p.Place);

        return rows.Select(r => new JobDto(
                r.Job.Id, r.Job.Kind, r.Job.State, r.Job.ImportId, r.Job.SnapshotId, r.Job.ExtractionId,
                places.TryGetValue(r.Job.Id, out var place) ? place : null,
                r.Job.RowsRead, r.Job.VotesDone, r.Job.VotesTotal, r.Job.Attempts, r.Job.LastError,
                TraceIdOf(r.Job.TraceParent), r.StartedBy,
                r.Job.CreatedAt, r.Job.StartedAt, r.Job.FinishedAt))
            .ToList();
    }

    /// <summary><c>00-{trace id}-{span id}-{flags}</c> → the trace id.</summary>
    private static string? TraceIdOf(string? traceParent) =>
        traceParent?.Split('-') is [_, var traceId, _, _] ? traceId : null;
}
