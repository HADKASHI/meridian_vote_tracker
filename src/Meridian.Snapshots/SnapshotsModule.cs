using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain.Scoring;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Meridian.Platform.Hosting;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Snapshots;

/// <summary>SNP: preview, snapshots, breakdowns, publication, and the comparison extra. Owned by slices 6 and 7.</summary>
public static class SnapshotsModule
{
    public static IServiceCollection AddSnapshotsModule(this IServiceCollection services) => services;

    /// <summary>The worker side: the snapshot job handler goes here (<c>services.AddJobHandler&lt;…&gt;()</c>).</summary>
    public static IServiceCollection AddSnapshotsJobs(this IServiceCollection services) =>
        services.AddJobHandler<SnapshotJobHandler>();

    public static IEndpointRouteBuilder MapSnapshotsModule(this IEndpointRouteBuilder api)
    {
        var snapshots = api.MapGroup("/snapshots").WithTags("Snapshots");

        snapshots.MapPost("/preview", async Task<Results<Ok<SnapshotPreviewDto>, ProblemHttpResult>> (
                    SnapshotSelectionDto selection, MeridianDbContext db, CancellationToken ct) =>
            {
                var votes = SnapshotSelection.Resolve(db, selection);
                var count = await votes.CountAsync(ct);
                if (count == 0) return NoVotesMatch();
                return TypedResults.Ok(new SnapshotPreviewDto(count,
                    await SnapshotSelection.NoPositionAsync(votes, ct),
                    await SnapshotSelection.WarningsAsync(db, votes, calculating: false, ct)));
            })
            .WithName("previewSnapshot")
            .WithSummary("Resolve a selection: vote count, votes without a position, and warnings. Runs no job.")
            .ProducesProblems(StatusCodes.Status400BadRequest)
            .ForAnalysts();

        snapshots.MapPost("", async Task<Results<Accepted<SnapshotCreatedDto>, ProblemHttpResult>> (
                    CreateSnapshotRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct) =>
            {
                var selection = request.Selection!;
                var votes = SnapshotSelection.Resolve(db, selection);
                if (!await votes.AnyAsync(ct)) return NoVotesMatch();
                var noPosition = await SnapshotSelection.NoPositionAsync(votes, ct);
                if (noPosition.Count > 0)
                    return ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.NoPosition,
                        "Votes without a position",
                        $"{noPosition.Count} selected {(noPosition.Count == 1 ? "vote has" : "votes have")} no position; set them or remove them.",
                        new Dictionary<string, object?> { ["votes"] = noPosition });

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var snapshot = new Snapshot
                {
                    Name = request.Name!.Trim(), Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    Selection = SnapshotJson.WriteSelection(selection), ScoringRulesVersion = AlignmentScoring.RulesVersion,
                    CreatedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                };
                db.Snapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
                var job = JobQueue.ForSnapshot(snapshot.Id);
                db.Jobs.Add(job);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return TypedResults.Accepted($"/api/snapshots/{snapshot.Id}", new SnapshotCreatedDto(snapshot.Id, job.Id));
            })
            .WithName("createSnapshot")
            .WithSummary("Create a snapshot; it is calculated as a job. Refused when no vote matches or a vote has no position.")
            // 400 is validation, no-votes-match or no-position; the body schema is the widest.
            .ProducesProblem<NoPositionProblem>(StatusCodes.Status400BadRequest)
            .ForAnalysts();

        snapshots.MapGet("", Results<Ok<PageDto<SnapshotSummaryDto>>, ProblemHttpResult> (int page = 1, int pageSize = 50) =>
                EndpointConventions.NotImplemented())
            .WithName("listSnapshots")
            .ForAnalysts().Stub();

        snapshots.MapGet("/compare", Results<Ok<SnapshotComparisonDto>, ProblemHttpResult> (long a, long b) =>
                EndpointConventions.NotImplemented())
            .WithName("compareSnapshots")
            .WithSummary("Each state's rank and score movement between two snapshots (slice 7).")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        snapshots.MapGet("/{snapshotId:long}", async Task<Results<Ok<SnapshotDto>, ProblemHttpResult>> (
                    long snapshotId, MeridianDbContext db, CancellationToken ct) =>
                await SnapshotQueries.GetAsync(db, snapshotId, ct) is { } snapshot
                    ? TypedResults.Ok(snapshot)
                    : NotFound("snapshot"))
            .WithName("getSnapshot")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        snapshots.MapGet("/{snapshotId:long}/states/{stateCode}", Results<Ok<SnapshotBreakdownDto>, ProblemHttpResult> (
                    long snapshotId, string stateCode) =>
                EndpointConventions.NotImplemented())
            .WithName("getSnapshotBreakdown")
            .WithSummary("One state's breakdown: every vote with its category and the exact value used.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        snapshots.MapPost("/{snapshotId:long}/publications", Results<Created<PublicationDto>, ProblemHttpResult> (
                    long snapshotId, PublishSnapshotRequest request) =>
                EndpointConventions.NotImplemented())
            .WithName("publishSnapshot")
            .WithSummary("Publish for a reporting quarter, or replace its publication with a reason.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<QuarterPublishedProblem>(StatusCodes.Status409Conflict)
            .ForAdmin().Stub();

        api.MapGet("/publications", Results<Ok<IReadOnlyList<QuarterPublicationsDto>>, ProblemHttpResult> () =>
                EndpointConventions.NotImplemented())
            .WithTags("Snapshots")
            .WithName("listPublications")
            .WithSummary("Each reporting quarter's chain of publications, newest quarter first.")
            .ForAnalysts().Stub();

        return api;
    }

    private static ProblemHttpResult NotFound(string what) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, $"No such {what}");

    private static ProblemHttpResult NoVotesMatch() =>
        ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.NoVotesMatch,
            "No votes match", "The selection matches no votes with approved details.");
}
