using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Imports;

/// <summary>ING: upload, the import job, and the import report. Owned by slice 1.</summary>
public static class ImportsModule
{
    public static IServiceCollection AddImportsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapImportsModule(this IEndpointRouteBuilder api)
    {
        var imports = api.MapGroup("/imports").WithTags("Imports");

        imports.MapPost("", Results<Accepted<ImportCreatedDto>, ProblemHttpResult> (IFormFile file) =>
                EndpointConventions.NotImplemented())
            .WithName("uploadImport")
            .WithSummary("Upload an export file; its import runs as a job.")
            .DisableAntiforgery()
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status413PayloadTooLarge)
            .ForAnalysts().Stub();

        imports.MapGet("", Results<Ok<PageDto<ImportSummaryDto>>, ProblemHttpResult> (int page = 1, int pageSize = 50) =>
                EndpointConventions.NotImplemented())
            .WithName("listImports")
            .ForAnalysts().Stub();

        imports.MapGet("/{importId:long}", Results<Ok<ImportDto>, ProblemHttpResult> (long importId) =>
                EndpointConventions.NotImplemented())
            .WithName("getImport")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        imports.MapGet("/{importId:long}/rows", Results<Ok<PageDto<ImportRowDto>>, ProblemHttpResult> (
                    long importId, Outcome? outcome, long? voteId, int page = 1, int pageSize = 100) =>
                EndpointConventions.NotImplemented())
            .WithName("listImportRows")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        imports.MapGet("/{importId:long}/votes", Results<Ok<IReadOnlyList<ImportVoteCountsDto>>, ProblemHttpResult> (long importId) =>
                EndpointConventions.NotImplemented())
            .WithName("listImportVoteCounts")
            .WithSummary("Counts per outcome for each vote the import touched.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        return api;
    }
}
