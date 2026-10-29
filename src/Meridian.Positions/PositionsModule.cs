using System.Security.Claims;
using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Meridian.Platform.Identity;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Positions;

/// <summary>POS: the positions worklist and setting a stance and weight. Owned by slice 5.</summary>
public static class PositionsModule
{
    public static IServiceCollection AddPositionsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapPositionsModule(this IEndpointRouteBuilder api)
    {
        api.MapGet("/positions", async Task<Results<Ok<PageDto<PositionWorklistItemDto>>, ProblemHttpResult>> (
                    MeridianDbContext db, CancellationToken ct, bool noPosition = false, int page = 1, int pageSize = 50) =>
                TypedResults.Ok(await PositionWorklist.PageAsync(db, noPosition, page, pageSize, ct)))
            .WithTags("Positions")
            .WithName("listPositions")
            .WithSummary("Votes with their stance and weight, votes without a position first.")
            .ForAnalysts();

        api.MapPut("/votes/{voteId:long}/position", async Task<Results<Ok<PositionDto>, ProblemHttpResult>> (
                    long voteId, SetPositionRequest request, ClaimsPrincipal principal, MeridianDbContext db,
                    CancellationToken ct) =>
                await PositionChanges.SaveAsync(db, voteId, request.Stance!.Value, request.Weight!.Value,
                        request.SeenRevisionId, principal.UserId(), ct) switch
                    {
                        PositionSaveResult.Saved saved => TypedResults.Ok(saved.Position),
                        PositionSaveResult.Stale stale => ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict,
                            ProblemTypes.StalePosition, "The position changed while you edited",
                            "Someone saved this vote's position after you opened it; your change was not saved.",
                            new Dictionary<string, object?> { ["current"] = stale.Current }),
                        _ => ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound,
                            "Vote not found", $"There is no vote {voteId}."),
                    })
            .WithTags("Positions")
            .WithName("setPosition")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<StalePositionProblem>(StatusCodes.Status409Conflict)
            .ForAdmin();

        return api;
    }
}
