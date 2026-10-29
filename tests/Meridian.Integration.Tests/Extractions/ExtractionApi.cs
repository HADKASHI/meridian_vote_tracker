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

    public static IHost Worker(ApiFactory api, IVoteSheetReader reader) =>
        WorkerHost.Build(api.ConnectionString, [], new Dictionary<string, string?>
        {
            ["Blob:ConnectionString"] = api.BlobConnectionString,
            ["Blob:Container"] = api.BlobContainer,
        }, services => services.AddExtractionsJobs().AddSingleton(reader));

    public static async Task RunJobsAsync(ApiFactory api, IVoteSheetReader reader)
    {
        using var worker = Worker(api, reader);
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

    public static SheetRead Sheet(SheetTotals? printed = null)
    {
        var rows = Members.Select((code, i) => (IReadOnlyList<SheetCell>)
        [
            new SheetCell(code, .99), new SheetCell(code, .98),
            new SheetCell(Choices[i] == Ballot.Y ? "X" : "", .95), new SheetCell(Choices[i] == Ballot.N ? "X" : "", .95),
            new SheetCell(Choices[i] == Ballot.A ? "X" : "", .95),
        ]).ToList();
        var p = printed ?? Printed;
        var lines = new[]
        {
            "MERIDIAN ASSEMBLY", "RECORD OF VOTE", "Vote No.:", "MA/2011/042", "Session:", "66", "Date of vote:", "4 October 2011",
            "Resolution:", "Protection of Cultural Heritage",
            $"Recorded totals: Yes: {p.Yes} No: {p.No} Abstentions: {p.Abstentions} Not voting: {p.NotVoting}",
        };
        return new SheetRead([new SheetPage(lines, [new SheetTable(rows)])]);
    }
}
