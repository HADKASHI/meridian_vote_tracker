using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>
/// PDF-02: the real reader, against a fake HTTP service and a hand-written response in Document Intelligence's
/// documented shape (analyze, then poll the operation; <c>analyzeResult</c> with pages, words, tables and cells).
/// No test calls Azure: that is the RealData smoke test.
/// </summary>
public sealed class DocumentIntelligenceTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private const string Endpoint = "https://meridian-test.cognitiveservices.azure.com/";
    private const string OperationUrl = "https://meridian-test.cognitiveservices.azure.com/documentintelligence/documentModels/prebuilt-layout/analyzeResults/op-1?api-version=2024-11-30";
    private static readonly byte[] Pdf = [.. "%PDF-1.4 synthetic"u8];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- a response builder: cells get consecutive offsets, words carry the confidences ----

    private sealed class Layout
    {
        private int _offset;
        public readonly JsonArray Words = [];
        public readonly JsonArray Tables = [];
        public readonly JsonArray Lines = [];

        public JsonObject Cell(int row, int column, string text, params double[] wordConfidences)
        {
            var spans = new JsonArray();
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                var span = new JsonObject { ["offset"] = _offset, ["length"] = words[i].Length };
                Words.Add(new JsonObject { ["content"] = words[i], ["confidence"] = wordConfidences[Math.Min(i, wordConfidences.Length - 1)], ["span"] = span.DeepClone() });
                spans.Add(span);
                _offset += words[i].Length + 1;
            }
            return new JsonObject { ["rowIndex"] = row, ["columnIndex"] = column, ["content"] = text, ["spans"] = spans };
        }

        public void Table(int pageNumber, int columns, params JsonObject[] cells) =>
            Tables.Add(new JsonObject
            {
                ["rowCount"] = cells.Max(c => c["rowIndex"]!.GetValue<int>()) + 1, ["columnCount"] = columns,
                ["boundingRegions"] = new JsonArray(new JsonObject { ["pageNumber"] = pageNumber }),
                ["cells"] = new JsonArray(cells.Select(c => (JsonNode)c).ToArray()),
            });

        public JsonObject Result(params string[][] linesPerPage)
        {
            var pages = new JsonArray();
            for (var p = 0; p < linesPerPage.Length; p++)
                pages.Add(new JsonObject
                {
                    ["pageNumber"] = p + 1,
                    ["lines"] = new JsonArray(linesPerPage[p].Select(l => (JsonNode)new JsonObject { ["content"] = l }).ToArray()),
                    ["words"] = p == 0 ? Words.DeepClone() : new JsonArray(),
                });
            return new JsonObject { ["status"] = "succeeded", ["analyzeResult"] = new JsonObject
            {
                ["apiVersion"] = "2024-11-30", ["modelId"] = "prebuilt-layout", ["pages"] = pages, ["tables"] = Tables.DeepClone(),
            } };
        }
    }

    private static JsonObject SmallSheet()
    {
        var l = new Layout();
        l.Table(1, 5,
            l.Cell(0, 0, "Code", .99), l.Cell(0, 1, "Member state", .99), l.Cell(0, 2, "YES", .99), l.Cell(0, 3, "NO", .99), l.Cell(0, 4, "ABST.", .99),
            l.Cell(1, 0, "ARG", .99), l.Cell(1, 1, "Argentina", .98), l.Cell(1, 2, "X", .95), l.Cell(1, 3, "", 1), l.Cell(1, 4, "", 1),
            l.Cell(2, 0, "BRA", .97), l.Cell(2, 1, "Brazil", .98), l.Cell(2, 2, "", 1), l.Cell(2, 3, "X x", .9, .41), l.Cell(2, 4, "", 1));
        return l.Result(
            ["MERIDIAN ASSEMBLY", "Vote No.: MA/2011/042", "Session: 66", "Date of vote: 4 October 2011",
             "Resolution: Protection of Cultural Heritage", "Recorded totals: Yes: 1 No: 1 Abstentions: 0 Not voting: 0"]);
    }

    // ---- the fake service ----

    private sealed class Stub(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;
        public readonly List<Captured> Requests = [];

        public sealed record Captured(HttpMethod Method, Uri Uri, string? Key, string? ContentType, byte[]? Body);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Captured(request.Method, request.RequestUri!,
                request.Headers.TryGetValues("Ocp-Apim-Subscription-Key", out var key) ? key.Single() : null,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken)));
            return responses[Math.Min(_next++, responses.Length - 1)](request);
        }
    }

    private static HttpResponseMessage Accepted(string operation = OperationUrl) =>
        new(HttpStatusCode.Accepted) { Headers = { { "Operation-Location", operation } } };

    private static HttpResponseMessage Json(JsonObject body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Running() => Json(new JsonObject { ["status"] = "running" });

    private sealed class CapturingLogger : ILogger<DocumentIntelligenceSheetReader>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static DocumentIntelligenceSheetReader Reader(Stub stub, CapturingLogger? logger = null) =>
        new(new HttpClient(stub), Options.Create(new DocumentIntelligenceOptions { Endpoint = Endpoint, Key = "secret-key", PollInterval = TimeSpan.Zero }),
            logger ?? new CapturingLogger());

    private static Task<SheetRead> ReadAsync(DocumentIntelligenceSheetReader reader, CancellationToken? token = null) =>
        reader.ReadAsync(new MemoryStream(Pdf), token ?? Ct);

    // ---- what is sent ----

    [Fact]
    public async Task The_pdf_is_posted_with_the_key_to_the_layout_model_and_the_operation_is_polled_until_it_succeeds()
    {
        var stub = new Stub(_ => Accepted(), _ => Running(), _ => Json(SmallSheet()));

        await ReadAsync(Reader(stub));

        Assert.Equal(3, stub.Requests.Count);
        var post = stub.Requests[0];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("https://meridian-test.cognitiveservices.azure.com/documentintelligence/documentModels/prebuilt-layout:analyze?api-version=2024-11-30",
            post.Uri.ToString());
        Assert.Equal(("secret-key", "application/pdf"), (post.Key, post.ContentType));
        Assert.Equal(Pdf, post.Body);
        Assert.All(stub.Requests.Skip(1), r => Assert.Equal((HttpMethod.Get, OperationUrl, "secret-key"), (r.Method, r.Uri.ToString(), r.Key)));
    }

    [Fact]
    public async Task An_operation_url_on_another_host_is_refused_so_the_key_is_never_sent_there()
    {
        var stub = new Stub(_ => Accepted("https://elsewhere.example.com/operations/1"));

        var error = await Assert.ThrowsAsync<SheetReaderException>(() => ReadAsync(Reader(stub)));

        Assert.True(error.Permanent);
        Assert.Single(stub.Requests);
    }

    // ---- what comes back ----

    [Fact]
    public async Task Table_cells_come_back_as_rows_with_their_text_and_the_confidence_of_their_least_certain_word()
    {
        var read = await ReadAsync(Reader(new Stub(_ => Accepted(), _ => Json(SmallSheet()))));

        var table = Assert.Single(Assert.Single(read.Pages).Tables);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["ARG", "Argentina", "X", "", ""], table.Rows[1].Select(c => c.Text));
        Assert.Equal(.95, table.Rows[1][2].Confidence);
        Assert.Equal(.41, table.Rows[2][3].Confidence);
        Assert.Equal(1.0, table.Rows[1][3].Confidence);
    }

    [Fact]
    public async Task The_mapper_reads_the_returned_lines_and_cells_as_the_header_the_totals_and_one_row_per_state()
    {
        var read = await ReadAsync(Reader(new Stub(_ => Accepted(), _ => Json(SmallSheet()))));

        var sheet = SheetMapper.Map(read);

        Assert.Equal(new SheetHeader("MA/2011/042", "66", "4 October 2011", "Protection of Cultural Heritage"), sheet.Header);
        Assert.Equal(new SheetTotals(1, 1, 0, 0), sheet.Printed);
        Assert.Equal([("ARG", Ballot.Y, false), ("BRA", Ballot.N, false)], sheet.Rows.Select(r => (r.Code, r.Choice!.Value, r.Ambiguous)).ToArray());
        Assert.Equal(.41, sheet.Rows[1].Confidence);
    }

    [Fact]
    public async Task A_table_belongs_to_the_page_its_region_names()
    {
        var l = new Layout();
        l.Table(2, 5, l.Cell(0, 0, "ARG", .9), l.Cell(0, 1, "Argentina", .9), l.Cell(0, 2, "X", .9), l.Cell(0, 3, "", 1), l.Cell(0, 4, "", 1));
        var result = l.Result(["first page"], ["second page"]);

        var read = await ReadAsync(Reader(new Stub(_ => Accepted(), _ => Json(result))));

        Assert.Equal([0, 1], read.Pages.Select(p => p.Tables.Count));
        Assert.Equal(["second page"], read.Pages[1].Lines);
    }

    // ---- when it goes wrong ----

    [Theory]
    [InlineData(400, true)]
    [InlineData(401, true)]
    [InlineData(403, true)]
    [InlineData(404, true)]
    [InlineData(413, true)]
    [InlineData(415, true)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    public async Task A_refusal_the_service_gives_is_permanent_and_a_busy_or_broken_service_is_worth_retrying(int status, bool permanent)
    {
        var stub = new Stub(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{\"error\":{\"message\":\"MA/2011/042 secret detail\"}}") });

        var error = await Assert.ThrowsAsync<SheetReaderException>(() => ReadAsync(Reader(stub)));

        Assert.Equal(permanent, error.Permanent);
        Assert.Contains(status.ToString(), error.Message);
        Assert.DoesNotContain("MA/2011/042", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task An_analysis_that_fails_names_the_error_code_and_no_more_and_is_permanent()
    {
        var failed = new JsonObject
        {
            ["status"] = "failed",
            ["error"] = new JsonObject { ["code"] = "InvalidContent", ["message"] = "The file for MA/2011/042 is corrupt." },
        };

        var error = await Assert.ThrowsAsync<SheetReaderException>(() => ReadAsync(Reader(new Stub(_ => Accepted(), _ => Json(failed)))));

        Assert.True(error.Permanent);
        Assert.Contains("InvalidContent", error.Message);
        Assert.DoesNotContain("MA/2011/042", error.Message);
    }

    [Fact]
    public async Task A_service_that_cannot_be_reached_is_worth_retrying()
    {
        var stub = new Stub(_ => throw new HttpRequestException("name resolution failed for meridian-test"));

        var error = await Assert.ThrowsAsync<SheetReaderException>(() => ReadAsync(Reader(stub)));

        Assert.False(error.Permanent);
        Assert.DoesNotContain("meridian-test", error.Message);
    }

    [Fact]
    public async Task A_cancelled_read_stops_polling_and_is_not_reported_as_a_failure_of_the_service()
    {
        using var source = new CancellationTokenSource();
        var stub = new Stub(_ => Accepted(), _ => { source.Cancel(); return Running(); }, _ => Running());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReadAsync(Reader(stub), source.Token));
    }

    [Fact]
    public async Task The_log_line_holds_the_model_the_outcome_and_the_time_never_the_sheet_or_the_key()
    {
        var logger = new CapturingLogger();

        await ReadAsync(Reader(new Stub(_ => Accepted(), _ => Json(SmallSheet())), logger));

        var line = Assert.Single(logger.Messages);
        Assert.Contains("prebuilt-layout", line);
        Assert.Contains("succeeded", line);
        Assert.Contains(" ms", line);
        Assert.DoesNotContain("MA/2011/042", line);
        Assert.DoesNotContain("Argentina", line);
        Assert.DoesNotContain("secret-key", line);
    }

    // ---- being configured ----

    [Fact]
    public async Task With_an_endpoint_and_a_key_the_worker_reads_sheets_with_the_service_and_without_them_it_fails_a_sheet_once_and_clearly()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        using (var configured = WorkerHost.Build(api.ConnectionString, [], Settings(api, endpoint: Endpoint, key: "k"), s => s.AddExtractionsJobs()))
        using (var scope = configured.Services.CreateScope())
            Assert.IsType<DocumentIntelligenceSheetReader>(scope.ServiceProvider.GetRequiredService<IVoteSheetReader>());

        using (var unconfigured = WorkerHost.Build(api.ConnectionString, [], Settings(api), s => s.AddExtractionsJobs()))
            await unconfigured.Processor().RunUntilIdleAsync(Ct);

        var extraction = await ExtractionApi.GetAsync(api.ClientAs("dana"), created.ExtractionId);
        Assert.Equal((ExtractionStatus.Failed, "No voting sheet reader is configured.", 1), (extraction.Status, extraction.Error, extraction.Job.Attempts));
    }

    private static Dictionary<string, string?> Settings(ApiFactory api, string? endpoint = null, string? key = null) => new()
    {
        ["Blob:ConnectionString"] = api.BlobConnectionString,
        ["Blob:Container"] = api.BlobContainer,
        ["DocumentIntelligence:Endpoint"] = endpoint,
        ["DocumentIntelligence:Key"] = key,
    };
}
