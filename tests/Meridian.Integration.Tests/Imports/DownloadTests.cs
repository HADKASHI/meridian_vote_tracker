using System.Text;
using Meridian.Integration.Tests.Extractions;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Meridian.Integration.Tests.Imports;

/// <summary>ADR 0009: the original file of an import comes back through the API, never through a link.</summary>
public sealed class DownloadTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private const string Csv = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot\nMA/2024/001,79,2024-01-10,A,BRA,Y\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_csv_import_downloads_as_an_attachment_with_its_name_and_content_type_and_the_bytes_uploaded()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv.Replace("\n", "\r\n"))).ToArray();
        var import = await ImportApi.ImportAsync(api, "q1 export.csv", bytes);

        var response = await api.ClientAs("omar").GetAsync($"/api/imports/{import.Id}/file", Ct);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("q1 export.csv", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task An_approved_voting_sheet_import_downloads_the_pdf_that_was_scanned()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var id = await ExtractionApi.ReadyForReviewAsync(api);
        var client = api.ClientAs("lena");
        var approved = await ExtractionApi.ApproveOkAsync(client, id);

        var response = await client.GetAsync($"/api/imports/{approved.ImportId}/file", Ct);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("MA_2011_042.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        var stored = await ExtractionApi.GetAsync(client, id);
        Assert.Equal(stored.SizeBytes, (await response.Content.ReadAsByteArrayAsync(Ct)).Length);
    }

    [Fact]
    public async Task An_unknown_import_is_a_not_found_problem()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/imports/999999/file", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task A_download_is_logged_with_its_id_and_size_and_never_the_content_or_the_name()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var import = await ImportApi.ImportAsync(api, "secret-name.csv", Csv);
        var logs = new CapturedLogs();
        await using var logged = api.WithWebHostBuilder(host => host.ConfigureLogging(l => l.AddProvider(logs)));
        var client = logged.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "dana");

        var response = await client.GetAsync($"/api/imports/{import.Id}/file", Ct);
        await response.Content.ReadAsByteArrayAsync(Ct);

        var line = Assert.Single(logs.Messages, m => m.Contains("download", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(import.Id.ToString(), line);
        Assert.Contains(Encoding.UTF8.GetByteCount(Csv).ToString(), line);
        Assert.DoesNotContain(logs.Messages, m => m.Contains("secret-name") || m.Contains("MA/2024/001"));
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages { get { lock (_messages) return [.. _messages]; } }

        public ILogger CreateLogger(string categoryName) => new Capture(this);

        public void Dispose() { }

        private sealed class Capture(CapturedLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner._messages) owner._messages.Add(formatter(state, exception));
            }
        }
    }
}
