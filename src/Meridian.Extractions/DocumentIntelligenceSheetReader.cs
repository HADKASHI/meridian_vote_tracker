using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Meridian.Domain.Sheets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.Extractions;

/// <summary>
/// Reads a sheet with Azure AI Document Intelligence (PDF-02): the PDF is posted to the layout model, the operation is
/// polled until it finishes, and its cells come back with their confidence. A refusal by the service is permanent,
/// a busy or unreachable one is worth retrying, and no message or log line holds the sheet's content or the key.
/// The call runs through an <see cref="HttpClient"/>, so it is a span in the job's trace.
/// </summary>
public sealed class DocumentIntelligenceSheetReader(
    HttpClient http, IOptions<DocumentIntelligenceOptions> options, ILogger<DocumentIntelligenceSheetReader> logger)
    : IVoteSheetReader
{
    private const string KeyHeader = "Ocp-Apim-Subscription-Key";

    public async Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try
        {
            var operation = await StartAsync(settings, pdf, cancellationToken);
            var result = await WaitAsync(settings, operation, cancellationToken);
            var sheet = DocumentIntelligenceResult.ToSheetRead(result);
            outcome = "succeeded";
            return sheet;
        }
        catch (SheetReaderException error)
        {
            outcome = error.Permanent ? "refused" : "unavailable";
            throw;
        }
        catch (HttpRequestException)
        {
            outcome = "unreachable";
            throw new SheetReaderException("The sheet reader could not be reached.", permanent: false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = "timed out";
            throw new SheetReaderException("The sheet reader did not answer in time.", permanent: false);
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            logger.LogInformation("Document Intelligence {Model} read {Outcome} in {ElapsedMs} ms",
                settings.ModelId, outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task<Uri> StartAsync(DocumentIntelligenceOptions settings, Stream pdf, CancellationToken cancellationToken)
    {
        var url = $"{settings.Endpoint!.TrimEnd('/')}/documentintelligence/documentModels/{settings.ModelId}:analyze?api-version={settings.ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StreamContent(pdf) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        request.Headers.Add(KeyHeader, settings.Key);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Accepted) throw Refusal(response.StatusCode);
        if (response.Headers.TryGetValues("Operation-Location", out var values) && Uri.TryCreate(values.First(), UriKind.Absolute, out var operation))
        {
            // The key goes only to the resource it belongs to, whatever the response says.
            if (!operation.Host.Equals(new Uri(settings.Endpoint).Host, StringComparison.OrdinalIgnoreCase))
                throw new SheetReaderException("The sheet reader answered with an address outside its own resource.", permanent: true);
            return operation;
        }
        throw new SheetReaderException("The sheet reader did not say where to find the result.", permanent: false);
    }

    private async Task<JsonElement> WaitAsync(DocumentIntelligenceOptions settings, Uri operation, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, operation);
            request.Headers.Add(KeyHeader, settings.Key);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Refusal(response.StatusCode);

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            switch (body.RootElement.GetProperty("status").GetString())
            {
                case "succeeded":
                    return body.RootElement.GetProperty("analyzeResult").Clone();
                case "failed":
                    var code = body.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var c) ? c.GetString() : null;
                    throw new SheetReaderException($"The sheet reader could not read the file ({code ?? "unknown error"}).", permanent: true);
                case "canceled":
                    throw new SheetReaderException("The sheet reader stopped the analysis.", permanent: false);
            }
            await Task.Delay(settings.PollInterval, cancellationToken);
        }
    }

    /// <summary>Only the status code is reported: the response body may quote the sheet.</summary>
    private static SheetReaderException Refusal(HttpStatusCode status)
    {
        var code = (int)status;
        return code is 408 or 429 or >= 500
            ? new SheetReaderException($"The sheet reader is busy or unavailable (HTTP {code}).", permanent: false)
            : code is 401 or 403
                ? new SheetReaderException($"The sheet reader rejected the credentials (HTTP {code}).", permanent: true)
                : new SheetReaderException($"The sheet reader refused the file (HTTP {code}).", permanent: true);
    }
}
