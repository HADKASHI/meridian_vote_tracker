using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Meridian.Platform.Hosting;
using Meridian.Platform.Http;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Meridian.Extractions;

/// <summary>PDF: scanned voting sheets read in the background and reviewed by a person (ADR 0008). Owned by slice 9.</summary>
public static class ExtractionsModule
{
    /// <summary>
    /// The worker side: the job handler, and the sheet reader. Document Intelligence reads sheets when
    /// <c>DocumentIntelligence:Endpoint</c> and <c>:Key</c> are set; otherwise a sheet fails once, saying so. A reader
    /// registered after this call (a test's fake) replaces both.
    /// </summary>
    public static IServiceCollection AddExtractionsJobs(this IServiceCollection services)
    {
        services.AddOptions<DocumentIntelligenceOptions>().BindConfiguration("DocumentIntelligence");
        services.AddHttpClient<DocumentIntelligenceSheetReader>();
        services.TryAddScoped<IVoteSheetReader>(provider =>
            provider.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value.IsConfigured
                ? provider.GetRequiredService<DocumentIntelligenceSheetReader>()
                : new NotConfiguredSheetReader());
        return services.AddMeridianBlobStorage().AddJobHandler<ExtractionJobHandler>();
    }

    public static IEndpointRouteBuilder MapExtractionsModule(this IEndpointRouteBuilder api)
    {
        var extractions = api.MapGroup("/extractions").WithTags("Extractions");
        var limit = new UploadLimit(ExtractionUpload.MaxUploadBytes(api.ServiceProvider.GetRequiredService<IConfiguration>()));

        extractions.MapPost("", ExtractionUpload.HandleAsync)
            .WithName("uploadExtraction")
            .WithSummary("Upload a scanned voting sheet; its extraction runs as a job.")
            .DisableAntiforgery()
            .WithMetadata(limit)
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status413PayloadTooLarge)
            .ForAnalysts();

        // The status is bound as text, because minimal APIs parse enums case-sensitively and the client sends the
        // glossary word (`needs_review`); the contract still names the enum.
        extractions.MapGet("", async Task<Results<Ok<PageDto<ExtractionSummaryDto>>, Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>> (
                    MeridianDbContext db, CancellationToken ct, [FromQuery] string? status, int page = 1, int pageSize = 50) =>
            {
                ExtractionStatus? filter = null;
                if (status is not null)
                {
                    filter = Enum.GetValues<ExtractionStatus>().Cast<ExtractionStatus?>()
                        .FirstOrDefault(s => SnakeCaseEnumConverter<ExtractionStatus>.ToName(s!.Value) == status);
                    if (filter is null)
                        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                            { ["status"] = ["Choose extracting, needs_review, failed, approved or rejected."] });
                }
                return TypedResults.Ok(await ExtractionQueries.ListAsync(db, filter, page, pageSize, ct));
            })
            .AddOpenApiOperationTransformer((operation, context, _) =>
            {
                var parameter = (OpenApiParameter)operation.Parameters!.Single(p => p.Name == "status");
                parameter.Schema = new OpenApiSchemaReference(nameof(ExtractionStatus), context.Document);
                return Task.CompletedTask;
            })
            .WithName("listExtractions")
            .WithSummary("Every extraction, newest first, with its status, job and issue counts; optionally one status only.")
            .ProducesProblems(StatusCodes.Status400BadRequest)
            .ForAnalysts();

        extractions.MapGet("/{extractionId:long}", async Task<Results<Ok<ExtractionDto>, NotFound>> (
                    long extractionId, MeridianDbContext db, CancellationToken ct) =>
                await ExtractionQueries.GetAsync(db, extractionId, ct) is { } extraction ? TypedResults.Ok(extraction) : TypedResults.NotFound())
            .WithName("getExtraction")
            .WithSummary("One extraction: the sheet as read, its counts against the printed totals and its issues.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        extractions.MapGet("/{extractionId:long}/file", async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> (
                    long extractionId, MeridianDbContext db, BlobContainerClient blobs, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var extraction = await db.Extractions.AsNoTracking().Where(e => e.Id == extractionId)
                    .Select(e => new { e.FileName, e.BlobName }).SingleOrDefaultAsync(ct);
                return await FileDownloads.StreamAsync(blobs, loggers.CreateLogger("Meridian.Extractions.Download"), "extraction", extractionId,
                    extraction is null ? null : (extraction.FileName, extraction.BlobName), ct);
            })
            .WithName("downloadExtractionFile")
            .WithSummary("The scanned voting sheet PDF of an extraction as an attachment.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        extractions.MapPost("/{extractionId:long}/edits", ExtractionReview.SaveEditsAsync)
            .WithName("saveExtractionEdits")
            .WithSummary("Correct rows, header fields or the printed totals; the sheet is checked again.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ProducesProblem<ExtractionAlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts();

        extractions.MapGet("/{extractionId:long}/preview", ExtractionPreview.HandleAsync)
            .WithName("previewExtraction")
            .WithSummary("What approving the sheet now would do, by the importer's own rules; nothing is written.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ProducesProblem<ExtractionAlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts();

        extractions.MapPost("/{extractionId:long}/approval", ExtractionApproval.ApproveAsync)
            .WithName("approveExtraction")
            .WithSummary("Approve a checked sheet: it is imported by the CSV's own rules. Exactly one of two simultaneous decisions applies.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ProducesProblem<ExtractionAlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts();

        extractions.MapPost("/{extractionId:long}/rejection", ExtractionReview.RejectAsync)
            .WithName("rejectExtraction")
            .WithSummary("Reject a sheet waiting for review. Exactly one of two simultaneous decisions applies.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ProducesProblem<ExtractionAlreadyDecidedProblem>(StatusCodes.Status409Conflict)
            .ForAnalysts();

        extractions.MapPost("/{extractionId:long}/retry", ExtractionRetry.HandleAsync)
            .WithName("retryExtraction")
            .WithSummary("Queue a failed extraction again.")
            .ProducesProblems(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)
            .ForAnalysts();

        return api;
    }
}
