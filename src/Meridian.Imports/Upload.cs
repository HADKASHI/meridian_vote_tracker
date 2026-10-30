using System.Security.Claims;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Platform.Http;
using Meridian.Platform.Jobs;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Imports;

/// <summary>
/// ING-01: the file goes to Blob while it is hashed, then the import and its job are
/// inserted in one transaction. The file is the evidence every outcome traces back to,
/// so it is never deleted.
/// </summary>
internal static class Upload
{
    public static async Task<Results<Accepted<ImportCreatedDto>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        IFormFile? file, ClaimsPrincipal user, MeridianDbContext db, BlobContainerClient blobs,
        UploadLimit limit, CancellationToken ct)
    {
        if (file is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Choose a file to upload."] });
        if (FileUploads.RefuseIfTooLarge(file, limit.MaxRequestBodySize!.Value, "imported") is { } tooLarge) return tooLarge;

        var importId = await FileUploads.NextIdAsync(db, ct);
        var stored = await FileUploads.StoreAsync(blobs, "imports", importId, file, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Imports.Add(new Import
        {
            Id = importId, FileName = stored.FileName, BlobName = stored.BlobName, Sha256 = stored.Sha256,
            SizeBytes = stored.SizeBytes, UploadedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
        });
        var job = JobQueue.ForImport(importId);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Accepted($"/api/imports/{importId}", new ImportCreatedDto(importId, job.Id));
    }
}
