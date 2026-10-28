using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
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

        api.MapPut("/votes/{voteId:long}/position", Results<Ok<PositionDto>, ProblemHttpResult> (
                    long voteId, SetPositionRequest request) =>
                EndpointConventions.NotImplemented())
            .WithTags("Positions")
            .WithName("setPosition")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<StalePositionProblem>(StatusCodes.Status409Conflict)
            .ForAdmin().Stub();

        return api;
    }
}
