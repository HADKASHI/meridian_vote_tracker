using Azure;
using Azure.Storage.Blobs;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Meridian.Platform.Storage;

/// <summary>
/// How a stored original goes back to a person, for every kind of upload (ADR 0009): streamed from Blob by
/// the API as an attachment, so the role checks apply and each download can be logged. There is no link
/// that works without them. Only ids and size are logged, never the file's name or content.
/// </summary>
public static class FileDownloads
{
    /// <summary>
    /// The download of <paramref name="stored"/> (the file name and blob name the record holds), a 404 when there is no such
    /// record (<paramref name="stored"/> is null) and a 404 problem if the blob itself is gone.
    /// </summary>
    public static async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> StreamAsync(
        BlobContainerClient blobs, ILogger logger, string kind, long id, (string FileName, string BlobName)? stored,
        CancellationToken cancellationToken)
    {
        if (stored is not var (fileName, blobName)) return TypedResults.NotFound();
        try
        {
            var download = await blobs.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Download of {Kind} {Id}: {SizeBytes} bytes", kind, id, download.Value.Details.ContentLength);
            return TypedResults.Stream(download.Value.Content, ContentTypeOf(fileName), fileName);
        }
        catch (RequestFailedException error) when (error.Status == StatusCodes.Status404NotFound)
        {
            logger.LogError("The stored file of {Kind} {Id} is missing from Blob", kind, id);
            return ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound,
                "The stored file is missing", $"The file of this {kind} is no longer in storage.");
        }
    }

    /// <summary>The upload does not keep a content type, so it follows the extension the file came with.</summary>
    public static string ContentTypeOf(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".csv" => "text/csv",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };
}
