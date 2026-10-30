using System.Diagnostics;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Platform.Hosting;
using Meridian.Platform.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meridian.AzureCheck;

public enum CheckStatus { Passed, Failed, NotConfigured }

/// <summary>One line of the operator's report. <see cref="Detail"/> never holds a secret or a sheet's content.</summary>
public sealed record CheckResult(string Name, CheckStatus Status, string Detail);

/// <summary>
/// The live checks of ADR 0009, run by the operator with their own <c>.env</c>: each uses the same code the app does
/// (the connection check, the Document Intelligence adapter, the sheet mapper) against the real service, and says
/// only what passed, with counts, versions and sizes.
/// </summary>
public static class LiveChecks
{
    /// <param name="serviceUri">A Blob service address: the account is then reached with the Entra ID sign-in (<c>az login</c>) and this wins over the connection string.</param>
    public static async Task<CheckResult> BlobAsync(
        string? connectionString, string? serviceUri, string container, CancellationToken cancellationToken, TokenCredential? credential = null)
    {
        const string Name = "Blob";
        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(serviceUri))
            return new(Name, CheckStatus.NotConfigured, "Neither BLOB_SERVICE_URI (Entra ID sign-in) nor BLOB_CONNECTION_STRING is set.");

        var entra = !string.IsNullOrWhiteSpace(serviceUri);
        var reached = await ConnectionCheck.RunAsync(null, connectionString, requireTls: false, cancellationToken,
            blobServiceUri: serviceUri, credential: credential);
        if (reached.Count > 0) return new(Name, CheckStatus.Failed, reached[0].Message);

        var bytes = new byte[64];
        Random.Shared.NextBytes(bytes);
        try
        {
            var blobs = BlobAccess.Service(connectionString, serviceUri, credential).GetBlobContainerClient(container);
            await blobs.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blob = blobs.GetBlobClient($"live-check/{Guid.NewGuid():N}.bin");
            try
            {
                await blob.UploadAsync(new BinaryData(bytes), cancellationToken);
                var back = (await blob.DownloadContentAsync(cancellationToken)).Value.Content.ToArray();
                return back.AsSpan().SequenceEqual(bytes)
                    ? new(Name, CheckStatus.Passed,
                        $"Uploaded and downloaded {bytes.Length} bytes in '{container}' ({(entra ? "Entra ID sign-in" : "connection string")}) and got the same bytes back; the test blob is deleted.")
                    : new(Name, CheckStatus.Failed, "The bytes downloaded differ from the bytes uploaded.");
            }
            finally
            {
                await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
        }
        catch (RequestFailedException error)
        {
            return new(Name, CheckStatus.Failed, $"Blob upload or download was refused ({error.Status} {error.ErrorCode}). " +
                (entra ? "The signed-in identity needs the Storage Blob Data Contributor role on the account." : "Check the account key and that the account allows this machine."));
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Name, CheckStatus.Failed, $"Blob upload or download failed: {error.GetType().Name}.");
        }
    }

    /// <summary>Reads one real sheet through <paramref name="reader"/> and says what the mapper made of it, in counts only.</summary>
    public static async Task<CheckResult> DocumentIntelligenceAsync(IVoteSheetReader? reader, string? pdfPath, CancellationToken cancellationToken)
    {
        const string Name = "Document Intelligence";
        if (reader is null)
            return new(Name, CheckStatus.NotConfigured, "DOCINTEL_ENDPOINT is not set.");
        if (string.IsNullOrWhiteSpace(pdfPath))
            return new(Name, CheckStatus.NotConfigured, "No voting sheet was given (--sheet <path to a real sheet PDF>).");
        if (!File.Exists(pdfPath)) return new(Name, CheckStatus.Failed, "The sheet file does not exist.");

        try
        {
            var started = Stopwatch.GetTimestamp();
            await using var pdf = File.OpenRead(pdfPath);
            var read = await reader.ReadAsync(pdf, cancellationToken);
            var sheet = SheetMapper.Map(read);
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            return new(Name, CheckStatus.Passed,
                $"Read {read.Pages.Count} page(s) in {seconds:0.0}s: {sheet.Rows.Count} grid rows, " +
                $"vote id {(sheet.Header.VoteId is null ? "not found" : "found")}, printed totals {(sheet.Printed is null ? "not found" : "found")}.");
        }
        catch (SheetReaderException error)
        {
            return new(Name, CheckStatus.Failed, $"The service {(error.Permanent ? "refused" : "could not answer")}: {error.Message}");
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Name, CheckStatus.Failed, $"The sheet could not be read: {error.GetType().Name}.");
        }
    }

    /// <summary>
    /// The timeout of each request to the service (the sheet's upload and each poll of its operation). It is per request
    /// and short on purpose: a service that stops answering must not hang the check, which the token still cancels as a whole.
    /// </summary>
    private static readonly TimeSpan SheetRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A reader for the endpoint, signing in with the key if there is one and with Entra ID (<c>az login</c>) if not.</summary>
    public static CheckSheetReader? CreateReader(string? endpoint, string? key, TokenCredential? credential = null)
    {
        var options = new DocumentIntelligenceOptions { Endpoint = endpoint, Key = key };
        if (!options.IsConfigured) return null;

        var http = new HttpClient { Timeout = SheetRequestTimeout };
        return new(http, new DocumentIntelligenceSheetReader(http, Options.Create(options), NullLogger<DocumentIntelligenceSheetReader>.Instance, credential));
    }
}

/// <summary>The sheet reader of a check. It owns the <see cref="HttpClient"/> it sends through, so disposing it releases that client.</summary>
public sealed class CheckSheetReader(HttpClient http, DocumentIntelligenceSheetReader reader) : IVoteSheetReader, IDisposable
{
    public Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken) => reader.ReadAsync(pdf, cancellationToken);

    public void Dispose() => http.Dispose();
}
