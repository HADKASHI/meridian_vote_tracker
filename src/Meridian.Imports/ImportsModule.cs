using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Hosting;
using Meridian.Platform.Http;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;

namespace Meridian.Imports;

/// <summary>ING: upload, the import job, and the import report. Owned by slice 1.</summary>
public static class ImportsModule
{
    public static IServiceCollection AddImportsModule(this IServiceCollection services)
    {
        services.AddScoped<ImportWriter>(); // also used by the approval of a voting sheet
        services.AddSingleton(provider => new UploadLimit(
            provider.GetRequiredService<IConfiguration>().GetValue("Imports:MaxUploadBytes", 200L * 1024 * 1024)));
        // The form reader buffers the file before the handler runs; the endpoint's own limit decides.
        services.AddOptions<FormOptions>().Configure<UploadLimit>((form, limit) =>
            form.MultipartBodyLengthLimit = limit.MaxRequestBodySize!.Value + 64 * 1024);
        return services;
    }

    /// <summary>The worker side: the import job handler goes here (<c>services.AddJobHandler&lt;…&gt;()</c>).</summary>
    public static IServiceCollection AddImportsJobs(this IServiceCollection services) =>
        services.AddMeridianBlobStorage().AddScoped<ImportWriter>().AddJobHandler<ImportJobHandler>();

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

        imports.MapGet("/{importId:long}/file", async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> (
                    long importId, MeridianDbContext db, BlobContainerClient blobs, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var import = await db.Imports.AsNoTracking().Where(i => i.Id == importId)
                    .Select(i => new { i.FileName, i.BlobName }).SingleOrDefaultAsync(ct);
                if (import is null) return TypedResults.NotFound();
                var download = await FileDownloads.StreamAsync(blobs, loggers.CreateLogger("Meridian.Imports.Download"), "import", importId, import.FileName, import.BlobName, ct);
                return download.Result switch
                {
                    FileStreamHttpResult file => file,
                    _ => (ProblemHttpResult)download.Result,
                };
            })
            .WithName("downloadImportFile")
            .WithSummary("The original file of an import (a CSV or a voting sheet PDF) as an attachment.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        // The outcome is bound as text, because minimal APIs parse enums case-sensitively and
        // the client sends the glossary word (`invalid`); the contract still names the enum.
        imports.MapGet("/{importId:long}/rows", async Task<Results<Ok<PageDto<ImportRowDto>>, NotFound, ProblemHttpResult>> (
                    long importId, MeridianDbContext db, CancellationToken ct,
                    [FromQuery] string? outcome, long? voteId, int page = 1, int pageSize = 100) =>
            {
                Outcome? filter = null;
                if (outcome is not null)
                {
                    if (!Enum.TryParse<Outcome>(outcome, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                        return TypedResults.Problem(new HttpValidationProblemDetails(new Dictionary<string, string[]>
                            { ["outcome"] = [$"Unknown outcome '{outcome}'."] }) { Status = StatusCodes.Status400BadRequest });
                    filter = parsed;
                }
                return await ImportQueries.RowsAsync(db, importId, filter, voteId, page, pageSize, ct) is { } rows
                    ? TypedResults.Ok(rows)
                    : TypedResults.NotFound();
            })
            .AddOpenApiOperationTransformer((operation, context, _) =>
            {
                var parameter = (OpenApiParameter)operation.Parameters!.Single(p => p.Name == "outcome");
                parameter.Schema = new OpenApiSchemaReference(nameof(Outcome), context.Document);
                return Task.CompletedTask;
            })
            .WithName("listImportRows")
            .WithSummary("The row records of an import in line order, filtered by outcome or vote.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ForAnalysts();

        imports.MapGet("/{importId:long}/votes", async Task<Results<Ok<IReadOnlyList<ImportVoteCountsDto>>, NotFound>> (
                    long importId, MeridianDbContext db, CancellationToken ct) =>
                await ImportQueries.VoteCountsAsync(db, importId, ct) is { } counts
                    ? TypedResults.Ok(counts)
                    : TypedResults.NotFound())
            .WithName("listImportVoteCounts")
            .WithSummary("Counts per outcome for each vote the import touched.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        return api;
    }
}
