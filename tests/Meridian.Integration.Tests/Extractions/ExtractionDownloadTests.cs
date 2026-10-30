using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>ADR 0009: a sheet waiting in Review can be checked against the exact file that was uploaded.</summary>
public sealed class ExtractionDownloadTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sheet_waiting_in_review_downloads_as_the_pdf_that_was_uploaded()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var bytes = (byte[])[.. System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 the scan")];
        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf", bytes);
        await ExtractionApi.RunJobsAsync(api, new FakeSheetReader(SheetFixtures.Sheet()));

        var response = await api.ClientAs("omar").GetAsync($"/api/extractions/{created.ExtractionId}/file", Ct);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("MA_2011_042.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task A_sheet_still_extracting_can_be_downloaded_too()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "queued.pdf");

        var response = await api.ClientAs("dana").GetAsync($"/api/extractions/{created.ExtractionId}/file", Ct);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(ExtractionApi.SheetBytes, await response.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task An_unknown_extraction_is_a_not_found_problem()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/extractions/999999/file", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }
}
