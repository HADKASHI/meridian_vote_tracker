using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Platform.Problems;

public static class ProblemDetailsSetup
{
    /// <summary>
    /// Every error is ProblemDetails with one of <see cref="ProblemTypes"/> and the trace
    /// id that finds it in the logs. Unexpected errors never carry their message.
    /// </summary>
    public static IServiceCollection AddMeridianProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            problem.Status = status;
            if (problem.Type is null || problem.Type.StartsWith("https://", StringComparison.Ordinal))
                problem.Type = ProblemTypes.ForStatus(status, problem is HttpValidationProblemDetails);
            if (status >= 500 && status != StatusCodes.Status501NotImplemented)
            {
                problem.Title = "Something went wrong";
                problem.Detail = null;
                problem.Extensions.Remove("exception");
            }
            problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier;
        });

    /// <summary>The 404 problem for a missing <paramref name="resource"/> (e.g. "vote"), naming its <paramref name="id"/> when known.</summary>
    public static ProblemHttpResult NotFound(string resource, object? id = null) =>
        Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, $"{char.ToUpperInvariant(resource[0])}{resource[1..]} not found",
            id is null ? null : $"There is no {resource} #{id}.");

    /// <summary>A problem result of the catalogue, for endpoints to return.</summary>
    public static ProblemHttpResult Problem(int status, string type, string title, string? detail = null,
        IDictionary<string, object?>? extensions = null) =>
        TypedResults.Problem(new ProblemDetails
        {
            Status = status, Type = type, Title = title, Detail = detail,
            Extensions = extensions ?? new Dictionary<string, object?>(),
        });
}
