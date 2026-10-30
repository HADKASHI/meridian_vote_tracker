using Meridian.Platform.Contracts;
using Meridian.Platform.Identity;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Platform.Http;

/// <summary>Marks an endpoint that still answers 501 until its module fills it in.</summary>
public sealed class StubMetadata;

public static class EndpointConventions
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Analysts and the admin; documents the 401.</summary>
    public static TBuilder ForAnalysts<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Policies.Analyst).WithProblem<TBuilder, Problem>(StatusCodes.Status401Unauthorized);

    /// <summary>The admin only; documents the 401 and 403.</summary>
    public static TBuilder ForAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Policies.Admin)
            .WithProblem<TBuilder, Problem>(StatusCodes.Status401Unauthorized)
            .WithProblem<TBuilder, Problem>(StatusCodes.Status403Forbidden);

    /// <summary>Documents a problem response with the plain problem body.</summary>
    public static RouteHandlerBuilder ProducesProblems(this RouteHandlerBuilder builder, params int[] statuses)
    {
        foreach (var status in statuses)
            builder = status == StatusCodes.Status400BadRequest
                ? builder.Produces<Contracts.ValidationProblem>(status, ProblemJson)
                : builder.Produces<Problem>(status, ProblemJson);
        return builder;
    }

    /// <summary>Documents a problem response with a typed body (<c>already-decided</c>, <c>stale-position</c>, …).</summary>
    public static RouteHandlerBuilder ProducesProblem<TProblem>(this RouteHandlerBuilder builder, int status)
        where TProblem : Problem =>
        builder.Produces<TProblem>(status, ProblemJson);

    /// <summary>Marks the endpoint as a stub; the role matrix test then expects 501 from it.</summary>
    public static RouteHandlerBuilder Stub(this RouteHandlerBuilder builder) => builder.WithMetadata(new StubMetadata());

    /// <summary>The 501 every stub returns.</summary>
    public static ProblemHttpResult NotImplemented() =>
        ProblemDetailsSetup.Problem(StatusCodes.Status501NotImplemented, ProblemTypes.NotImplemented,
            "Not implemented yet", "This endpoint is part of the contract; its slice has not been built yet.");

    private static TBuilder WithProblem<TBuilder, TProblem>(this TBuilder builder, int status)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ProducesResponseTypeMetadata(status, typeof(TProblem), [ProblemJson]));
}
