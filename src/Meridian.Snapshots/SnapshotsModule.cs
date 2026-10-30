using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Data.Migrations;
using Meridian.Domain;
using Meridian.Domain.Scoring;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Meridian.Platform.Hosting;
using Meridian.Platform.Identity;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Meridian.Snapshots;

/// <summary>Preview, snapshots, breakdowns, publication, and the comparison extra.</summary>
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
                ToHttp(await CreateSnapshot.RunAsync(db, request, user, ct)))
            .WithName("createSnapshot")
            .WithSummary("Create a snapshot; it is calculated as a job. Refused when its name is taken, no vote matches or a vote has no position.")
            // 400 is validation, no-votes-match or no-position; the body schema is the widest.
            .ProducesProblem<NoPositionProblem>(StatusCodes.Status400BadRequest)
            .ProducesProblems(StatusCodes.Status403Forbidden, StatusCodes.Status409Conflict)
            .ForAnalysts();

        snapshots.MapGet("", async Task<Results<Ok<PageDto<SnapshotSummaryDto>>, ProblemHttpResult>> (
                    MeridianDbContext db, CancellationToken ct, int page = 1, int pageSize = 50) =>
                TypedResults.Ok(await SnapshotQueries.ListAsync(db, Math.Max(page, 1), Math.Clamp(pageSize, 1, 200), ct)))
            .WithName("listSnapshots")
            .ForAnalysts();

        snapshots.MapGet("/compare", async Task<Results<Ok<SnapshotComparisonDto>, ProblemHttpResult>> (
                    long a, long b, MeridianDbContext db, CancellationToken ct) =>
            {
                var (first, second) = (await SnapshotQueries.RefAsync(db, a, ct), await SnapshotQueries.RefAsync(db, b, ct));
                if (first is null || second is null) return NotFound("snapshot");
                if (!await SnapshotQueries.AllCalculatedAsync(db, [.. new HashSet<long> { a, b }], ct))
                    return ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
                        "Not calculated", "Only calculated snapshots can be compared.");
                return TypedResults.Ok(await SnapshotQueries.CompareAsync(db, first, second, ct));
            })
            .WithName("compareSnapshots")
            .WithSummary("Each state's rank and score movement between two snapshots, read from what they stored.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ForAnalysts();

        snapshots.MapGet("/{snapshotId:long}", async Task<Results<Ok<SnapshotDto>, ProblemHttpResult>> (
                    long snapshotId, MeridianDbContext db, CancellationToken ct) =>
                await SnapshotQueries.GetAsync(db, snapshotId, ct) is { } snapshot
                    ? TypedResults.Ok(snapshot)
                    : NotFound("snapshot"))
            .WithName("getSnapshot")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        snapshots.MapGet("/{snapshotId:long}/states/{stateCode}", async Task<Results<Ok<SnapshotBreakdownDto>, ProblemHttpResult>> (
                    long snapshotId, string stateCode, MeridianDbContext db, CancellationToken ct) =>
                await SnapshotQueries.BreakdownAsync(db, snapshotId, stateCode.ToUpperInvariant(), ct) is { } breakdown
                    ? TypedResults.Ok(breakdown)
                    : NotFound("member state in this snapshot"))
            .WithName("getSnapshotBreakdown")
            .WithSummary("One state's breakdown: every vote with its category and the exact value used.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        snapshots.MapGet("/{snapshotId:long}/changes", async Task<Results<Ok<SnapshotChangesDto>, ProblemHttpResult>> (
                    long snapshotId, MeridianDbContext db, CancellationToken ct) =>
            {
                if (await SnapshotQueries.RefAsync(db, snapshotId, ct) is null) return NotFound("snapshot");
                if (!await SnapshotQueries.AllCalculatedAsync(db, [snapshotId], ct))
                    return ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
                        "Not calculated", "Only a calculated snapshot can be checked for changes.");
                return TypedResults.Ok(await SnapshotDrift.ChangesAsync(db, snapshotId, ct));
            })
            .WithName("getSnapshotChanges")
            .WithSummary("What would differ if the snapshot were created now. A live read; no job, score or rank.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ForAnalysts();

        snapshots.MapPost("/{snapshotId:long}/publications", async Task<Results<Created<PublicationDto>, ProblemHttpResult>> (
                    long snapshotId, PublishSnapshotRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct) =>
                ToHttp(await PublishSnapshot.RunAsync(db, snapshotId, request, user.UserId(), ct)))
            .WithName("publishSnapshot")
            .WithSummary("Publish for a reporting quarter, or replace its publication with a reason.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<QuarterPublishedProblem>(StatusCodes.Status409Conflict)
            .ForAdmin();

        api.MapGet("/publications", async Task<Results<Ok<IReadOnlyList<QuarterPublicationsDto>>, ProblemHttpResult>> (
                    MeridianDbContext db, CancellationToken ct) =>
                TypedResults.Ok<IReadOnlyList<QuarterPublicationsDto>>(await PublicationQueries.QuartersAsync(db, ct)))
            .WithTags("Snapshots")
            .WithName("listPublications")
            .WithSummary("Each reporting quarter's chain of publications, newest quarter first.")
            .ForAnalysts();

        api.MapGet("/rule-sets", () => TypedResults.Ok<IReadOnlyList<RuleSetDto>>([.. RuleSets.All.Select(RuleSetDto.Of)]))
            .WithTags("Snapshots")
            .WithName("listRuleSets")
            .WithSummary("The known rule sets, the default first. Only the admin may create a snapshot under another.")
            .ForAnalysts();

        return api;
    }

    private static Results<Accepted<SnapshotCreatedDto>, ProblemHttpResult> ToHttp(CreateSnapshot.Outcome outcome) => outcome switch
    {
        CreateSnapshot.Created created => TypedResults.Accepted($"/api/snapshots/{created.SnapshotId}",
            new SnapshotCreatedDto(created.SnapshotId, created.JobId)),
        CreateSnapshot.Forbidden => ProblemDetailsSetup.Problem(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden,
            "Forbidden", "Only the admin chooses a rule set other than the default."),
        CreateSnapshot.NameTaken taken => NameTaken(taken.Name),
        CreateSnapshot.NoVotesMatch => NoVotesMatch(),
        CreateSnapshot.NoPosition missing => ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.NoPosition,
            "Votes without a position",
            $"{missing.Votes.Count} selected {(missing.Votes.Count == 1 ? "vote has" : "votes have")} no position; set them or remove them.",
            new Dictionary<string, object?> { ["votes"] = missing.Votes }),
        _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
    };

    private static Results<Created<PublicationDto>, ProblemHttpResult> ToHttp(PublishSnapshot.Outcome outcome) => outcome switch
    {
        PublishSnapshot.Created created => TypedResults.Created("/api/publications", created.Publication),
        PublishSnapshot.NotFound => NotFound("snapshot"),
        PublishSnapshot.NotCalculated => ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
            "Not calculated", "Only a calculated snapshot can be published."),
        PublishSnapshot.NothingToReplace nothing => ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
            "Nothing to replace", $"{nothing.Quarter} has no published snapshot to replace."),
        PublishSnapshot.QuarterPublished published => QuarterPublished(published.Current),
        _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
    };

    private static ProblemHttpResult QuarterPublished(PublicationDto current) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.QuarterPublished,
            "The quarter is already published",
            $"{current.Quarter} is published with {current.SnapshotName}; replace it to publish another snapshot.",
            new Dictionary<string, object?> { ["publication"] = current });

    private static ProblemHttpResult NameTaken(string name) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.Conflict, "Snapshot name taken",
            $"A snapshot named \"{name}\" already exists; choose another name.",
            new Dictionary<string, object?> { ["name"] = name });

    private static ProblemHttpResult NotFound(string what) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, $"No such {what}");

    private static ProblemHttpResult NoVotesMatch() =>
        ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.NoVotesMatch,
            "No votes match", "The selection matches no votes with approved details.");
}
