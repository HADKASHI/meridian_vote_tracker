using System.Net.Http.Headers;
using System.Net.Http.Json;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Imports;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>Calls the extractions endpoints the way the Angular client does, and runs the worker with a fake reader.</summary>
public static class ExtractionApi
{
    /// <summary>Not a real scan: the fake reader never looks at the bytes, the store keeps them verbatim.</summary>
    public static readonly byte[] SheetBytes = [.. "%PDF-1.4 synthetic voting sheet"u8];

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[]? content = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content ?? SheetBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/extractions", form, TestContext.Current.CancellationToken);
    }

    public static async Task<ExtractionCreatedDto> UploadOkAsync(HttpClient client, string fileName, byte[]? content = null)
    {
        var response = await UploadAsync(client, fileName, content);
        Assert.Equal(202, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ExtractionCreatedDto>(ImportApi.Json, TestContext.Current.CancellationToken))!;
    }

    public static Task<ExtractionDto> GetAsync(HttpClient client, long id) =>
        ImportApi.GetAsync<ExtractionDto>(client, $"/api/extractions/{id}");

    /// <summary>Uploads a distinct sheet, runs its job with <paramref name="sheet"/> and returns it waiting for review.</summary>
    public static async Task<long> ReadyForReviewAsync(ApiFactory api, SheetRead? sheet = null, string name = "MA_2011_042.pdf")
    {
        var created = await UploadOkAsync(api.ClientAs("dana"), name, [.. System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4 {Guid.NewGuid()}")]);
        await RunJobsAsync(api, new FakeSheetReader(sheet ?? SheetFixtures.Sheet()));
        return created.ExtractionId;
    }

    public static Task<HttpResponseMessage> SaveEditsAsync(HttpClient client, long id, SaveEditsRequest request) =>
        client.PostAsJsonAsync($"/api/extractions/{id}/edits", request, ImportApi.Json, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RejectAsync(HttpClient client, long id, string? comment = null) =>
        client.PostAsJsonAsync($"/api/extractions/{id}/rejection", new RejectExtractionRequest(comment), ImportApi.Json,
            TestContext.Current.CancellationToken);

    public static async Task<ExtractionDto> SaveEditsOkAsync(HttpClient client, long id, SaveEditsRequest request)
    {
        var response = await SaveEditsAsync(client, id, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<ExtractionDto>(ImportApi.Json, TestContext.Current.CancellationToken))!;
    }

    public static Task<HttpResponseMessage> RetryAsync(HttpClient client, long id) =>
        client.PostAsync($"/api/extractions/{id}/retry", null, TestContext.Current.CancellationToken);

    public static IHost Worker(ApiFactory api, IVoteSheetReader reader, IDictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>
        {
            ["Blob:ConnectionString"] = api.BlobConnectionString,
            ["Blob:Container"] = api.BlobContainer,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>()) all[key] = value;
        return WorkerHost.Build(api.ConnectionString, [], all, services => services.AddExtractionsJobs().AddSingleton(reader));
    }

    public static async Task RunJobsAsync(ApiFactory api, IVoteSheetReader reader, IDictionary<string, string?>? settings = null)
    {
        using var worker = Worker(api, reader, settings);
        await worker.Processor().RunUntilIdleAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>Returns a sheet the test built and keeps the bytes it was given.</summary>
public sealed class FakeSheetReader(SheetRead sheet) : IVoteSheetReader
{
    public byte[]? Received { get; private set; }

    public async Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await pdf.CopyToAsync(copy, cancellationToken);
        Received = copy.ToArray();
        return sheet;
    }
}

/// <summary>
/// A sheet for the test database's members on 4 October 2011 (ARG BRA CIV FRA ISL JPN MNE NZL SSD TLS TUR),
/// shaped like the Assembly's: header, code / state / YES / NO / ABST. grid, recorded totals.
/// </summary>
public static class SheetFixtures
{
    public static readonly string[] Members = ["ARG", "BRA", "CIV", "FRA", "ISL", "JPN", "MNE", "NZL", "SSD", "TLS", "TUR"];

    public static readonly Ballot[] Choices =
        [Ballot.Y, Ballot.Y, Ballot.N, Ballot.Y, Ballot.A, Ballot.Y, Ballot.N, Ballot.X, Ballot.Y, Ballot.A, Ballot.N];

    /// <summary>Counted from <see cref="Choices"/>: 5 yes, 3 no, 2 abstentions, 1 not voting.</summary>
    public static readonly SheetTotals Printed = new(5, 3, 2, 1);

    /// <param name="tweak">Changes one row's cells by its index, as a misread would.</param>
    /// <param name="omitTotals">The footer could not be read.</param>
    public static SheetRead Sheet(SheetTotals? printed = null, Action<int, SheetCell[]>? tweak = null,
        string date = "4 October 2011", bool omitTotals = false)
    {
        var rows = Members.Select((code, i) =>
        {
            var cells = new[]
            {
                new SheetCell(code, .99), new SheetCell(code, .98),
                new SheetCell(Choices[i] == Ballot.Y ? "X" : "", .95), new SheetCell(Choices[i] == Ballot.N ? "X" : "", .95),
                new SheetCell(Choices[i] == Ballot.A ? "X" : "", .95),
            };
            tweak?.Invoke(i, cells);
            return (IReadOnlyList<SheetCell>)cells;
        }).ToList();
        var p = printed ?? Printed;
        var lines = new List<string>
        {
            "MERIDIAN ASSEMBLY", "RECORD OF VOTE", "Vote No.:", "MA/2011/042", "Session:", "66", "Date of vote:", date,
            "Resolution:", "Protection of Cultural Heritage",
        };
        if (!omitTotals) lines.Add($"Recorded totals: Yes: {p.Yes} No: {p.No} Abstentions: {p.Abstentions} Not voting: {p.NotVoting}");
        return new SheetRead([new SheetPage(lines, [new SheetTable(rows)])]);
    }
}

/// <summary>Fails the way the test says on each call, then reads the sheet; counts its calls.</summary>
public sealed class FlakySheetReader(Func<int, Exception?> failure, SheetRead sheet) : IVoteSheetReader
{
    public int Calls { get; private set; }

    public Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken)
    {
        Calls++;
        return failure(Calls) is { } error ? throw error : Task.FromResult(sheet);
    }
}
