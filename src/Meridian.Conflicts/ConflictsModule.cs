using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Conflicts;

public enum ConflictFilter { Pending, Decided, All }

/// <summary>CON: the Review inbox and conflict decisions. Owned by slice 3.</summary>
public static class ConflictsModule
{
    public static IServiceCollection AddConflictsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapConflictsModule(this IEndpointRouteBuilder api)
    {
        var conflicts = api.MapGroup("/conflicts").WithTags("Conflicts");

        conflicts.MapGet("", Results<Ok<PageDto<ConflictListItemDto>>, ProblemHttpResult> (
                    ConflictFilter state = ConflictFilter.Pending, long[]? voteIds = null, long? importId = null,
                    int page = 1, int pageSize = 50) =>
                EndpointConventions.NotImplemented())
            .WithName("listConflicts")
            .WithSummary("The Review inbox: conflicts ordered by vote, then subject.")
            .ForAnalysts().Stub();

        conflicts.MapGet("/{conflictId:long}", Results<Ok<ConflictDto>, ProblemHttpResult> (long conflictId) =>
                EndpointConventions.NotImplemented())
            .WithName("getConflict")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        conflicts.MapPost("/{conflictId:long}/decision", Results<Ok<ConflictDecisionDto>, ProblemHttpResult> (
                    long conflictId, DecideConflictRequest request) =>
                EndpointConventions.NotImplemented())
            .WithName("decideConflict")
            .WithSummary("Accept or reject a conflict. Exactly one of two simultaneous decisions applies.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<AlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts().Stub();

        return api;
    }
}
