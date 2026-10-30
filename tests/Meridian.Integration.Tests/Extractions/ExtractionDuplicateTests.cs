using System.Net.Http.Json;
using Meridian.Extractions;
using Meridian.Integration.Tests.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>Uploading the same sheet twice is one extraction, whoever uploads it and however fast.</summary>
public sealed class ExtractionDuplicateTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_same_bytes_again_return_the_existing_extraction_instead_of_a_second_one()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var first = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        var response = await ExtractionApi.UploadAsync(api.ClientAs("omar"), "copy-of-the-same-sheet.pdf");

        Assert.Equal(200, (int)response.StatusCode);
        var again = (await response.Content.ReadFromJsonAsync<ExtractionCreatedDto>(ImportApi.Json, Ct))!;
        Assert.Equal((first.ExtractionId, first.JobId, true), (again.ExtractionId, again.JobId, again.Existing));
        var page = await ImportApi.GetAsync<PageDto<ExtractionSummaryDto>>(api.ClientAs("dana"), "/api/extractions");
        Assert.Equal("dana", Assert.Single(page.Items).UploadedBy);
    }

    [Fact]
    public async Task A_first_upload_says_it_is_not_a_repeat()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        Assert.False((await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf")).Existing);
    }

    [Fact]
    public async Task Different_bytes_are_a_different_extraction_even_with_the_same_name()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var first = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf");

        var second = await ExtractionApi.UploadOkAsync(api.ClientAs("dana"), "MA_2011_042.pdf", [.. "%PDF-1.4 a corrigendum"u8]);

        Assert.NotEqual(first.ExtractionId, second.ExtractionId);
    }

    [Fact]
    public async Task Two_uploads_of_the_same_sheet_at_once_make_one_extraction()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        for (var round = 0; round < 5; round++)
        {
            var bytes = (byte[])[.. System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4 race round {round}")];

            var results = await RaceBarrier.RunTogetherAsync(2, async _ =>
            {
                var response = await ExtractionApi.UploadAsync(api.ClientAs("dana"), "race.pdf", bytes);
                Assert.True(response.IsSuccessStatusCode, $"answered {(int)response.StatusCode}");
                return (await response.Content.ReadFromJsonAsync<ExtractionCreatedDto>(ImportApi.Json, Ct))!;
            });

            Assert.Equal(results[0].ExtractionId, results[1].ExtractionId);
            Assert.Equal(1, results.Count(r => !r.Existing));
        }
    }
}
