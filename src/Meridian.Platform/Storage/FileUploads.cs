using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Meridian.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Platform.Storage;

/// <summary>An uploaded file as kept: where it lives, its SHA-256 and its size.</summary>
public sealed record StoredFile(string FileName, string BlobName, string Sha256, long SizeBytes);

/// <summary>
/// How an uploaded file is kept, for every kind of upload (CSV exports, scanned sheets): an id from the shared
/// history sequence, then the bytes streamed to Blob while they are hashed. The file is the evidence everything
/// traces back to, so it is never deleted.
/// </summary>
public static class FileUploads
{
    public static Task<long> NextIdAsync(MeridianDbContext db, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<long>($"SELECT nextval('history_seq') AS \"Value\"").SingleAsync(cancellationToken);

    /// <summary>The SHA-256 of the file as it will be stored, to find an earlier upload of the same bytes.</summary>
    public static async Task<string> HashAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(source, cancellationToken));
    }

    /// <summary>The 413 for a file over <paramref name="maxBytes"/>, or null when it fits.</summary>
    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult? RefuseIfTooLarge(IFormFile file, long maxBytes, string verb) =>
        file.Length > maxBytes
            ? Problems.ProblemDetailsSetup.Problem(StatusCodes.Status413PayloadTooLarge, Problems.ProblemTypes.PayloadTooLarge,
                "The file is too large", $"Files up to {maxBytes / (1024 * 1024)} MB can be {verb}.")
            : null;

    /// <summary>Streams <paramref name="file"/> to <c>{folder}/{id}/{name}</c> while hashing it.</summary>
    public static async Task<StoredFile> StoreAsync(
        BlobContainerClient blobs, string folder, long id, IFormFile file, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(file.FileName);
        var blobName = $"{folder}/{id}/{fileName}";

        using var sha = SHA256.Create();
        await using (var source = file.OpenReadStream())
        await using (var hashing = new CryptoStream(source, sha, CryptoStreamMode.Read))
            await blobs.GetBlobClient(blobName).UploadAsync(hashing, cancellationToken);

        return new StoredFile(fileName, blobName, Convert.ToHexStringLower(sha.Hash!), file.Length);
    }
}
