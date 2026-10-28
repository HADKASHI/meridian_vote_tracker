using System.Security.Claims;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
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
        if (file.Length > limit.MaxRequestBodySize)
            return ProblemDetailsSetup.Problem(StatusCodes.Status413PayloadTooLarge, ProblemTypes.PayloadTooLarge,
                "The file is too large", $"Files up to {limit.MaxRequestBodySize / (1024 * 1024)} MB can be imported.");

        var importId = await db.Database.SqlQuery<long>($"SELECT nextval('history_seq') AS \"Value\"").SingleAsync(ct);
        var fileName = Path.GetFileName(file.FileName);
        var blobName = $"imports/{importId}/{fileName}";

        using var sha = SHA256.Create();
        await using (var source = file.OpenReadStream())
        await using (var hashing = new CryptoStream(source, sha, CryptoStreamMode.Read))
            await blobs.GetBlobClient(blobName).UploadAsync(hashing, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Imports.Add(new Import
        {
            Id = importId, FileName = fileName, BlobName = blobName, Sha256 = Convert.ToHexStringLower(sha.Hash!),
            SizeBytes = file.Length, UploadedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
        });
        var job = JobQueue.ForImport(importId);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Accepted($"/api/imports/{importId}", new ImportCreatedDto(importId, job.Id));
    }
}

/// <summary><c>Imports:MaxUploadBytes</c> as the upload endpoint's request size limit.</summary>
internal sealed class UploadLimit(long maxBytes) : IRequestSizeLimitMetadata
{
    public long? MaxRequestBodySize => maxBytes;
}
