using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;

namespace Meridian.Integration.Tests.Imports;

public sealed class UploadTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private const string Csv = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot\nMA/2024/001,79,2024-01-10,A,BRA,Y\n";

    [Fact]
    public async Task An_upload_is_accepted_with_its_import_and_a_queued_job()
    {
        using var tracing = Tracing.ListenToRequests();
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");

        var created = await ImportApi.UploadOkAsync(client, "q1.csv", Csv);

        var import = await ImportApi.GetAsync<ImportDto>(client, $"/api/imports/{created.ImportId}");
        Assert.Equal("q1.csv", import.FileName);
        Assert.Equal("dana", import.UploadedBy);
        Assert.Equal(Encoding.UTF8.GetByteCount(Csv), import.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Csv))), import.Sha256);
        Assert.Equal(created.JobId, import.Job.Id);
        Assert.Equal(JobState.Queued, import.Job.State);
        Assert.Equal(1, import.Job.PlaceInQueue);
        Assert.False(string.IsNullOrEmpty(import.Job.TraceId));
    }

    [Fact]
    public async Task The_uploaded_file_is_kept_in_blob_storage_byte_for_byte()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv.Replace("\n", "\r\n"))).ToArray();

        var response = await ImportApi.UploadAsync(api.ClientAs("dana"), "q1.csv", bytes);
        Assert.Equal(202, (int)response.StatusCode);
        var created = (await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<ImportCreatedDto>(
            response.Content, ImportApi.Json, TestContext.Current.CancellationToken))!;

        var blob = new BlobContainerClient(azurite.ConnectionString, api.BlobContainer)
            .GetBlobClient($"imports/{created.ImportId}/q1.csv");
        var stored = (await blob.DownloadContentAsync(TestContext.Current.CancellationToken)).Value.Content.ToArray();
        Assert.Equal(bytes, stored);
    }

    [Fact]
    public async Task Imports_are_listed_newest_first_with_their_job_and_counts()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var first = await ImportApi.UploadOkAsync(client, "first.csv", Csv);
        var second = await ImportApi.UploadOkAsync(api.ClientAs("omar"), "second.csv", Csv);

        var page = await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(client, "/api/imports");

        Assert.Equal([second.ImportId, first.ImportId], page.Items.Select(i => i.Id));
        Assert.Equal(2, page.Total);
        Assert.Equal(["omar", "dana"], page.Items.Select(i => i.UploadedBy));
        Assert.Equal([2, 1], page.Items.Select(i => i.Job.PlaceInQueue ?? 0));
        Assert.All(page.Items, i => Assert.Equal(new OutcomeCountsDto(0, 0, 0, 0, 0, 0), i.Counts));
    }

    [Fact]
    public async Task An_upload_without_a_file_is_a_validation_problem()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").PostAsync("/api/imports",
            new MultipartFormDataContent { { new StringContent("x"), "other" } }, TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("file", out _));
    }

    [Fact]
    public async Task A_file_over_the_upload_limit_is_refused_with_413()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var limited = api.WithSettings(new() { ["Imports:MaxUploadBytes"] = "1024" });

        var response = await ImportApi.UploadAsync(limited.ClientAs("dana"), "big.csv", new string('x', 4096));

        await response.AssertProblemAsync(413, "payload-too-large");
    }

    [Fact]
    public async Task An_unknown_import_is_not_found()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/imports/999999", TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(404, "not-found");
    }
}
