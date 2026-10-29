using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
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
                    CreatedBy = user.UserId(),
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
                var calculated = await db.Jobs.Where(j => (j.SnapshotId == a || j.SnapshotId == b) && j.State == JobState.Done)
                    .CountAsync(ct);
                if (calculated < (a == b ? 1 : 2))
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

        snapshots.MapPost("/{snapshotId:long}/publications", async Task<Results<Created<PublicationDto>, ProblemHttpResult>> (
                    long snapshotId, PublishSnapshotRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct) =>
                await PublishAsync(snapshotId, request, user.UserId(), db, ct))
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

        return api;
    }

    /// <summary>
    /// SNP-05: a quarter has one published snapshot. A first publication needs an empty
    /// quarter; a replacement names the quarter's current publication and gives a reason.
    /// The partial unique indexes decide a race: the loser gets 409 with the winner.
    /// </summary>
    private static async Task<Results<Created<PublicationDto>, ProblemHttpResult>> PublishAsync(
        long snapshotId, PublishSnapshotRequest request, string userId, MeridianDbContext db, CancellationToken ct)
    {
        var state = await db.Jobs.Where(j => j.SnapshotId == snapshotId).Select(j => (JobState?)j.State).SingleOrDefaultAsync(ct);
        if (state is null) return NotFound("snapshot");
        if (state != JobState.Done)
            return ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
                "Not calculated", "Only a calculated snapshot can be published.");

        var quarter = request.Quarter!;
        var current = await PublicationQueries.CurrentAsync(db, quarter, ct);
        if (current is not null && current.Id != request.SupersedesPublicationId) return QuarterPublished(current);
        if (current is null && request.SupersedesPublicationId is not null)
            return ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.BadRequest,
                "Nothing to replace", $"{quarter} has no published snapshot to replace.");

        var publication = new Publication
        {
            SnapshotId = snapshotId, Quarter = quarter, PublishedBy = userId,
            SupersedesPublicationId = request.SupersedesPublicationId, Reason = request.Reason?.Trim(),
        };
        db.Publications.Add(publication);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return QuarterPublished((await PublicationQueries.CurrentAsync(db, quarter, ct))!);
        }

        var created = await PublicationQueries.Load(db, db.Publications.Where(p => p.Id == publication.Id)).SingleAsync(ct);
        return TypedResults.Created($"/api/publications", created);
    }

    private static ProblemHttpResult QuarterPublished(PublicationDto current) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.QuarterPublished,
            "The quarter is already published",
            $"{current.Quarter} is published with {current.SnapshotName}; replace it to publish another snapshot.",
            new Dictionary<string, object?> { ["publication"] = current });

    private static ProblemHttpResult NotFound(string what) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, $"No such {what}");

    private static ProblemHttpResult NoVotesMatch() =>
        ProblemDetailsSetup.Problem(StatusCodes.Status400BadRequest, ProblemTypes.NoVotesMatch,
            "No votes match", "The selection matches no votes with approved details.");
}
