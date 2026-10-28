using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Hosting;
using Meridian.Platform.Http;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Imports;

/// <summary>ING: upload, the import job, and the import report. Owned by slice 1.</summary>
public static class ImportsModule
{
    public static IServiceCollection AddImportsModule(this IServiceCollection services)
    {
        services.AddSingleton(provider => new UploadLimit(
            provider.GetRequiredService<IConfiguration>().GetValue("Imports:MaxUploadBytes", 200L * 1024 * 1024)));
        // The form reader buffers the file before the handler runs; the endpoint's own limit decides.
        services.AddOptions<FormOptions>().Configure<UploadLimit>((form, limit) =>
            form.MultipartBodyLengthLimit = limit.MaxRequestBodySize!.Value + 64 * 1024);
        return services;
    }

    /// <summary>The worker side: the import job handler goes here (<c>services.AddJobHandler&lt;…&gt;()</c>).</summary>
    public static IServiceCollection AddImportsJobs(this IServiceCollection services) =>
        services.AddMeridianBlobStorage().AddJobHandler<ImportJobHandler>();

    public static IEndpointRouteBuilder MapImportsModule(this IEndpointRouteBuilder api)
    {
        var imports = api.MapGroup("/imports").WithTags("Imports");

        imports.MapPost("", Upload.HandleAsync)
            .WithName("uploadImport")
            .WithSummary("Upload an export file; its import runs as a job.")
            .DisableAntiforgery()
            .WithMetadata(api.ServiceProvider.GetRequiredService<UploadLimit>())
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status413PayloadTooLarge)
            .ForAnalysts();

        imports.MapGet("", async Task<Ok<PageDto<ImportSummaryDto>>> (MeridianDbContext db, CancellationToken ct,
                    int page = 1, int pageSize = 50) =>
                TypedResults.Ok(await ImportQueries.ListAsync(db, page, pageSize, ct)))
            .WithName("listImports")
            .WithSummary("Every import, newest first, with its job and counts per outcome.")
            .ForAnalysts();

        imports.MapGet("/{importId:long}", async Task<Results<Ok<ImportDto>, NotFound>> (
                    long importId, MeridianDbContext db, CancellationToken ct) =>
                await ImportQueries.GetAsync(db, importId, ct) is { } import ? TypedResults.Ok(import) : TypedResults.NotFound())
            .WithName("getImport")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

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
