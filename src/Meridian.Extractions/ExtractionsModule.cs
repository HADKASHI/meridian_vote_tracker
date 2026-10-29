using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Hosting;
using Meridian.Platform.Http;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meridian.Extractions;

/// <summary>PDF: scanned voting sheets read in the background and reviewed by a person (ADR 0008). Owned by slice 9.</summary>
public static class ExtractionsModule
{
    /// <summary>
    /// The worker side: the job handler, and a stand-in sheet reader that a real one replaces
    /// (<c>services.AddSingleton&lt;IVoteSheetReader, …&gt;()</c> after this call wins).
    /// </summary>
    public static IServiceCollection AddExtractionsJobs(this IServiceCollection services)
    {
        services.TryAddScoped<IVoteSheetReader, NotConfiguredSheetReader>();
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

        extractions.MapGet("", async Task<Ok<PageDto<ExtractionSummaryDto>>> (MeridianDbContext db, CancellationToken ct,
                    int page = 1, int pageSize = 50) =>
                TypedResults.Ok(await ExtractionQueries.ListAsync(db, page, pageSize, ct)))
            .WithName("listExtractions")
            .WithSummary("Every extraction, newest first, with its status, job and issue counts.")
            .ForAnalysts();

        extractions.MapGet("/{extractionId:long}", async Task<Results<Ok<ExtractionDto>, NotFound>> (
                    long extractionId, MeridianDbContext db, CancellationToken ct) =>
                await ExtractionQueries.GetAsync(db, extractionId, ct) is { } extraction ? TypedResults.Ok(extraction) : TypedResults.NotFound())
            .WithName("getExtraction")
            .WithSummary("One extraction: the sheet as read, its counts against the printed totals and its issues.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        extractions.MapPost("/{extractionId:long}/edits", ExtractionReview.SaveEditsAsync)
            .WithName("saveExtractionEdits")
            .WithSummary("Correct rows, header fields or the printed totals; the sheet is checked again.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
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
