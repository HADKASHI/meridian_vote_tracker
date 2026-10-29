using System.Security.Claims;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Jobs;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Meridian.Extractions;

/// <summary>
/// PDF-01: the sheet goes to Blob while it is hashed (as every upload does), then the extraction and its job
/// are inserted in one transaction. The request returns at once; the job reads the sheet in the background.
/// </summary>
internal static class ExtractionUpload
{
    public const long DefaultMaxUploadBytes = 30L * 1024 * 1024;

    public static long MaxUploadBytes(IConfiguration configuration) =>
        configuration.GetValue("Extractions:MaxUploadBytes", DefaultMaxUploadBytes);

    public static async Task<Results<Accepted<ExtractionCreatedDto>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        IFormFile? file, ClaimsPrincipal user, MeridianDbContext db, BlobContainerClient blobs,
        IConfiguration configuration, CancellationToken ct)
    {
        if (file is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Choose a voting sheet to upload."] });
        if (FileUploads.RefuseIfTooLarge(file, MaxUploadBytes(configuration), "read") is { } tooLarge) return tooLarge;

        var id = await FileUploads.NextIdAsync(db, ct);
        var stored = await FileUploads.StoreAsync(blobs, "sheets", id, file, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Extractions.Add(new Extraction
        {
            Id = id, FileName = stored.FileName, BlobName = stored.BlobName, Sha256 = stored.Sha256,
            SizeBytes = stored.SizeBytes, UploadedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Status = ExtractionStatus.Extracting,
        });
        var job = JobQueue.ForExtraction(id);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Accepted($"/api/extractions/{id}", new ExtractionCreatedDto(id, job.Id));
    }
}
