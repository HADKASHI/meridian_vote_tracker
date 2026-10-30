using System.Net.Http.Json;
using System.Text.Json;
using Azure.Storage.Blobs;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>PDF-01: an analyst uploads a sheet, the request returns at once and extraction runs in the background.</summary>
public sealed class ExtractionUploadTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_upload_is_accepted_at_once_with_its_extraction_and_a_queued_job()
    {
        using var tracing = Tracing.ListenToRequests();
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");

        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.Extracting, extraction.Status);
        Assert.Equal("MA_2011_042.pdf", extraction.FileName);
        Assert.Equal("dana", extraction.UploadedBy);
        Assert.Equal(ExtractionApi.SheetBytes.Length, extraction.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(ExtractionApi.SheetBytes)), extraction.Sha256);
        Assert.Equal((created.JobId, JobState.Queued, 1), (extraction.Job.Id, extraction.Job.State, extraction.Job.PlaceInQueue));
        Assert.Equal(extraction.Id, extraction.Job.ExtractionId);
        Assert.Equal(JobKind.PdfExtraction, extraction.Job.Kind);
        Assert.False(string.IsNullOrEmpty(extraction.Job.TraceId));
        Assert.Empty(extraction.Rows);
    }

    [Fact]
    public async Task The_uploaded_sheet_is_kept_in_blob_storage_byte_for_byte_beside_the_imports()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var created = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        var blob = new BlobContainerClient(azurite.ConnectionString, api.BlobContainer)
            .GetBlobClient($"sheets/{created.ExtractionId}/MA_2011_042.pdf");
        Assert.Equal(ExtractionApi.SheetBytes, (await blob.DownloadContentAsync(Ct)).Value.Content.ToArray());
    }

    [Fact]
    public async Task Uploading_nothing_is_a_validation_problem_on_the_file_field()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").PostAsync("/api/extractions",
            new MultipartFormDataContent { { new StringContent("x"), "other" } }, Ct);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("file", out _));
    }

    [Fact]
    public async Task Extractions_are_listed_newest_first_with_their_status_and_uploader()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var first = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "first.pdf", [.. "%PDF-1.4 first"u8]);
        var second = await ExtractionApi.UploadOkAsync(api.ClientAs("omar"), "second.pdf", [.. "%PDF-1.4 second"u8]);

        var page = await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(api.ClientAs("dana"), "/api/extractions");

        Assert.Equal([second.ExtractionId, first.ExtractionId], page.Items.Select(i => i.Id));
        Assert.Equal(["omar", "dana"], page.Items.Select(i => i.UploadedBy));
        Assert.All(page.Items, i => Assert.Equal(ExtractionStatus.Extracting, i.Status));
    }

    [Fact]
    public async Task An_unknown_extraction_is_404()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/extractions/999", Ct);

        await response.AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task A_sheet_over_the_upload_limit_is_refused_with_413()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var limited = api.WithSettings(new() { ["Extractions:MaxUploadBytes"] = "1024" });

        var response = await ExtractionApi.UploadAsync(limited.ClientAs("dana"), "big.pdf", new byte[4096]);

        await response.AssertProblemAsync(413, "payload-too-large");
    }

    [Fact]
    public async Task The_job_runs_the_reader_on_the_stored_sheet_and_leaves_the_extraction_to_review_with_its_rows()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        var reader = new FakeSheetReader(SheetFixtures.Sheet());

        await ExtractionApi.RunJobsAsync(api, reader);

        Assert.Equal(ExtractionApi.SheetBytes, reader.Received);
        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.NeedsReview, extraction.Status);
        Assert.Equal(JobState.Done, extraction.Job.State);
        Assert.Equal(new SheetHeader("MA/2011/042", "66", "4 October 2011", "Protection of Cultural Heritage"), extraction.Header);
        Assert.Equal(SheetFixtures.Members, extraction.Rows.Select(r => r.Code));
        Assert.Equal(SheetFixtures.Choices, extraction.Rows.Select(r => r.Choice!.Value));
        Assert.Equal((SheetFixtures.Printed, SheetFixtures.Printed), (extraction.Counted, extraction.Printed));
        Assert.Empty(extraction.Issues);
        Assert.True(extraction.CanApprove);
        Assert.Equal("MA/2011/042", extraction.VoteId);
    }

    [Fact]
    public async Task A_sheet_whose_counts_differ_from_its_printed_totals_reaches_review_blocked()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");

        await ExtractionApi.RunJobsAsync(api, new FakeSheetReader(SheetFixtures.Sheet(new SheetTotals(5, 3, 1, 1))));

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.NeedsReview, extraction.Status);
        Assert.Equal([SheetIssueKind.TotalsMismatch], extraction.Issues.Select(i => i.Kind));
        Assert.False(extraction.CanApprove);
        var summary = (await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(client, "/api/extractions")).Items.Single();
        Assert.Equal((1, 0), (summary.BlockingIssues, summary.Warnings));
    }

    [Fact]
    public async Task The_extraction_job_shows_in_the_job_list_and_the_status_poll_under_its_own_kind()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");

        var status = await client.GetFromJsonAsync<JsonElement>("/api/status", Ct);
        var queued = Assert.Single(status.GetProperty("queued").EnumerateArray());
        Assert.Equal("pdf_extraction", queued.GetProperty("kind").GetString());
        Assert.Equal(created.ExtractionId, queued.GetProperty("extractionId").GetInt64());
        Assert.Equal("dana", queued.GetProperty("startedBy").GetString());

        await ExtractionApi.RunJobsAsync(api, new FakeSheetReader(SheetFixtures.Sheet()));

        var jobs = await client.GetFromJsonAsync<JsonElement>("/api/jobs", Ct);
        Assert.Equal("done", Assert.Single(jobs.GetProperty("items").EnumerateArray()).GetProperty("state").GetString());
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_status_where_failed_includes_a_job_that_failed()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var waiting = await ExtractionApi.UploadOkAsync(client, "waiting.pdf", [.. "%PDF-1.4 waiting"u8]);
        var broken = await ExtractionApi.UploadOkAsync(client, "broken.pdf", [.. "%PDF-1.4 broken"u8]);
        await ExtractionApi.RunJobsAsync(api, new FlakySheetReader(call => call == 1 ? null : new HttpRequestException("503"), SheetFixtures.Sheet()));
        var reading = await ExtractionApi.UploadOkAsync(client, "reading.pdf", [.. "%PDF-1.4 reading"u8]);

        async Task<long[]> Ids(string status) =>
            (await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(client, $"/api/extractions?status={status}")).Items.Select(i => i.Id).ToArray();

        Assert.Equal([waiting.ExtractionId], await Ids("needs_review"));
        Assert.Equal([reading.ExtractionId], await Ids("extracting"));
        Assert.Equal([broken.ExtractionId], await Ids("failed"));
        Assert.Empty(await Ids("approved"));
        Assert.Equal(3, (await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(client, "/api/extractions")).Total);
        Assert.Equal(1, (await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(client, "/api/extractions?status=needs_review&pageSize=1")).Total);
    }

    [Fact]
    public async Task An_unknown_status_filter_is_a_validation_problem_on_status()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/extractions?status=nope", Ct);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("status", out _));
    }
}
