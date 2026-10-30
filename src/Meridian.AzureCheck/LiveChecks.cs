using System.Diagnostics;
using Azure;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Platform.Hosting;
using Meridian.Platform.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Trace;

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
    public static readonly string[] SupportedMajors = ["17", "18"];

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
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(Name, CheckStatus.Failed, $"Blob upload or download failed: {error.GetType().Name}.");
        }
    }

    public static async Task<CheckResult> DatabaseAsync(
        string? connectionString, CancellationToken cancellationToken, bool requireTls = true, TokenCredential? credential = null)
    {
        const string Name = "Database";
        if (string.IsNullOrWhiteSpace(connectionString))
            return new(Name, CheckStatus.NotConfigured, "AZURE_POSTGRES_CONNECTION_STRING is not set.");

        var reached = await ConnectionCheck.RunAsync(connectionString, null, requireTls, cancellationToken, credential: credential);
        if (reached.Count > 0) return new(Name, CheckStatus.Failed, reached[0].Message);

        var entra = DatabaseAccess.UsesEntra(connectionString);
        try
        {
            await using var dataSource = entra ? DatabaseAccess.EntraDataSource(connectionString, credential ?? AzureCredentials.Default) : null;
            await using var connection = dataSource is null ? new NpgsqlConnection(connectionString) : dataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT current_setting('server_version'), (SELECT ssl FROM pg_stat_ssl WHERE pid = pg_backend_pid())", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var version = reader.GetString(0);
            var tls = !reader.IsDBNull(1) && reader.GetBoolean(1);
            var major = version.Split('.')[0];
            var detail = $"PostgreSQL {version}, TLS {(tls ? "on" : "off")}, {(entra ? "Entra ID sign-in" : "password")}.";
            if (!SupportedMajors.Contains(major))
                return new(Name, CheckStatus.Failed, $"{detail} Supported versions are {string.Join(" and ", SupportedMajors)}.");
            if (requireTls && !tls) return new(Name, CheckStatus.Failed, $"{detail} TLS is required.");
            return new(Name, CheckStatus.Passed, detail);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(Name, CheckStatus.Failed, $"The database answered the connection but not the version query: {error.GetType().Name}.");
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
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new(Name, CheckStatus.Failed, $"The sheet could not be read: {error.GetType().Name}.");
        }
    }

    /// <summary>A reader for the endpoint, signing in with the key if there is one and with Entra ID (<c>az login</c>) if not.</summary>
    public static IVoteSheetReader? CreateReader(string? endpoint, string? key, TokenCredential? credential = null)
    {
        var options = new DocumentIntelligenceOptions { Endpoint = endpoint, Key = key };
        return options.IsConfigured
            ? new DocumentIntelligenceSheetReader(new HttpClient(), Options.Create(options), NullLogger<DocumentIntelligenceSheetReader>.Instance, credential)
            : null;
    }

    private static bool IsInsightsConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
        return parts.TryGetValue("InstrumentationKey", out var key) && Guid.TryParse(key, out _)
            && (!parts.TryGetValue("IngestionEndpoint", out var endpoint)
                || (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));
    }

    /// <summary>Sends one trace. Application Insights accepts it without an answer the exporter can read, so the report says where to look.</summary>
    public static Task<CheckResult> ApplicationInsightsAsync(string? connectionString, CancellationToken cancellationToken)
    {
        const string Name = "Application Insights";
        if (string.IsNullOrWhiteSpace(connectionString))
            return Task.FromResult(new CheckResult(Name, CheckStatus.NotConfigured, "APPLICATIONINSIGHTS_CONNECTION_STRING is not set."));

        if (!IsInsightsConnectionString(connectionString))
            return Task.FromResult(new CheckResult(Name, CheckStatus.Failed,
                "The connection string is not InstrumentationKey=<guid>;IngestionEndpoint=https://...; copy it from the Application Insights overview page."));

        const string Source = "Meridian.AzureCheck";
        try
        {
            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(Source)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString)
                .Build();
            using var activity = new ActivitySource(Source).StartActivity("meridian-live-check");
            activity?.Stop();
            return Task.FromResult(provider!.ForceFlush(15_000)
                ? new CheckResult(Name, CheckStatus.Passed, "Sent one trace named 'meridian-live-check'; it appears under Transaction search in the portal within a few minutes.")
                : new CheckResult(Name, CheckStatus.Failed, "The trace could not be sent in time. Check the connection string and the network."));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return Task.FromResult(new CheckResult(Name, CheckStatus.Failed, $"The connection string was not accepted: {error.GetType().Name}."));
        }
    }
}
