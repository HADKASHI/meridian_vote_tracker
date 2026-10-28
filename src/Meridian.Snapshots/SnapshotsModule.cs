using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Snapshots;

/// <summary>SNP: preview, snapshots, breakdowns, publication, and the comparison extra. Owned by slices 6 and 7.</summary>
public static class SnapshotsModule
{
    public static IServiceCollection AddSnapshotsModule(this IServiceCollection services) => services;

    /// <summary>The worker side: the snapshot job handler goes here (<c>services.AddJobHandler&lt;…&gt;()</c>).</summary>
    public static IServiceCollection AddSnapshotsJobs(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapSnapshotsModule(this IEndpointRouteBuilder api)
    {
        var snapshots = api.MapGroup("/snapshots").WithTags("Snapshots");

        snapshots.MapPost("/preview", Results<Ok<SnapshotPreviewDto>, ProblemHttpResult> (SnapshotSelectionDto selection) =>
                EndpointConventions.NotImplemented())
            .WithName("previewSnapshot")
            .WithSummary("Resolve a selection: vote count, votes without a position, and warnings. Runs no job.")
            .ProducesProblems(StatusCodes.Status400BadRequest)
            .ForAnalysts().Stub();

        snapshots.MapPost("", Results<Accepted<SnapshotCreatedDto>, ProblemHttpResult> (CreateSnapshotRequest request) =>
                EndpointConventions.NotImplemented())
            .WithName("createSnapshot")
            .WithSummary("Create a snapshot; it is calculated as a job. Refused when no vote matches or a vote has no position.")
            // 400 is validation, no-votes-match or no-position; the body schema is the widest.
            .ProducesProblem<NoPositionProblem>(StatusCodes.Status400BadRequest)
            .ForAnalysts().Stub();

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

        snapshots.MapGet("/{snapshotId:long}", Results<Ok<SnapshotDto>, ProblemHttpResult> (long snapshotId) =>
                EndpointConventions.NotImplemented())
            .WithName("getSnapshot")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

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
}
