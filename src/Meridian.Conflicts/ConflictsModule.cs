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
using ValidationProblem = Microsoft.AspNetCore.Http.HttpResults.ValidationProblem;

namespace Meridian.Conflicts;

public enum ConflictFilter { Pending, Decided, All }

/// <summary>The Review inbox and conflict decisions.</summary>
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

        conflicts.MapGet("/{conflictId:long}", async Task<Results<Ok<ConflictDto>, ProblemHttpResult>> (
                    long conflictId, MeridianDbContext db, CancellationToken ct) =>
                await ReviewInbox.GetAsync(db, conflictId, ct) is { } conflict
                    ? TypedResults.Ok(conflict)
                    : NotFound(conflictId))
            .WithName("getConflict")
            .WithSummary("The decision pane: both values with their origins, the decision and losing attempts.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        conflicts.MapPost("/{conflictId:long}/decision", async Task<Results<Ok<ConflictDecisionDto>, ProblemHttpResult>> (
                    long conflictId, DecideConflictRequest request, MeridianDbContext db, ClaimsPrincipal user,
                    CancellationToken ct) =>
                await DecideConflict.RunAsync(db, conflictId, request.Decision!.Value,
                        user.UserId(), request.Comment, ct) switch
                {
                    DecideConflict.Decided decided => TypedResults.Ok(decided.Decision),
                    DecideConflict.AlreadyDecided lost => AlreadyDecided(lost.Winner),
                    _ => NotFound(conflictId),
                })
            .WithName("decideConflict")
            .WithSummary("Accept or reject a conflict. Exactly one of two simultaneous decisions applies.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<AlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts();

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

    private static ProblemHttpResult AlreadyDecided(ConflictDecisionDto winner) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.AlreadyDecided, "Already decided",
            $"{winner.DecidedBy} decided this conflict first. Your decision was not applied and is on the vote's timeline.",
            new Dictionary<string, object?> { ["decision"] = winner });

    private static ProblemHttpResult NotFound(long conflictId) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, "Conflict not found",
            $"There is no conflict #{conflictId}.");

    private static ValidationProblem InvalidFilter() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["state"] = ["Choose pending, decided or all."],
        });
}
