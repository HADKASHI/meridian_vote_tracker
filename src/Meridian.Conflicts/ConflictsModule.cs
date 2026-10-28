using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ValidationProblem = Microsoft.AspNetCore.Http.HttpResults.ValidationProblem;

namespace Meridian.Conflicts;

public enum ConflictFilter { Pending, Decided, All }

/// <summary>CON: the Review inbox and conflict decisions. Owned by slice 3.</summary>
public static class ConflictsModule
{
    public static IServiceCollection AddConflictsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapConflictsModule(this IEndpointRouteBuilder api)
    {
        var conflicts = api.MapGroup("/conflicts").WithTags("Conflicts");

        conflicts.MapGet("", async Task<Results<Ok<PageDto<ConflictListItemDto>>, ValidationProblem>> (
                    MeridianDbContext db, CancellationToken ct,
                    string? state = null, long[]? voteIds = null, long? importId = null,
                    int page = 1, int pageSize = 50) =>
                ParseFilter(state) is { } filter
                    ? TypedResults.Ok(await ReviewInbox.ListAsync(db, filter, voteIds, importId, page, pageSize, ct))
                    : InvalidFilter())
            .WithName("listConflicts")
            .WithSummary("The Review inbox: conflicts ordered by vote, then subject.")
            .ProducesProblems(StatusCodes.Status400BadRequest)
            .WithEnumQuery<ConflictFilter>("state", "pending")
            .ForAnalysts();

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

    /// <summary>
    /// Query enums bind case-sensitively, so <c>state</c> is read as text and parsed as
    /// its glossary word (<c>pending</c>, <c>decided</c>, <c>all</c>), as the contract spells it.
    /// </summary>
    private static ConflictFilter? ParseFilter(string? state) =>
        state is null ? ConflictFilter.Pending
        : Enum.GetValues<ConflictFilter>().Cast<ConflictFilter?>()
            .FirstOrDefault(f => SnakeCaseEnumConverter<ConflictFilter>.ToName(f!.Value) == state);

    private static ValidationProblem InvalidFilter() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["state"] = ["Choose pending, decided or all."],
        });
}
