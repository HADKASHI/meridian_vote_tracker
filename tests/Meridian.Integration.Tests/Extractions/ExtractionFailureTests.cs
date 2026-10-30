using Meridian.Domain;
using Meridian.Extractions;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>PDF-06: AI failures are shown to the user, never crash, and can be tried again.</summary>
public sealed class ExtractionFailureTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static readonly byte[] NotAPdf = [.. "just some text"u8];

    private static FlakySheetReader Good() => new(_ => null, SheetFixtures.Sheet());

    [Fact]
    public async Task A_file_that_is_not_a_pdf_fails_the_extraction_without_calling_the_reader_or_retrying()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "notes.pdf", NotAPdf);
        var reader = Good();

        await ExtractionApi.RunJobsAsync(api, reader);

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal((ExtractionStatus.Failed, "The file is not a PDF."), (extraction.Status, extraction.Error));
        Assert.Equal((0, 1), (reader.Calls, extraction.Job.Attempts));
        Assert.Empty(extraction.Rows);
    }

    [Fact]
    public async Task An_empty_file_fails_the_extraction()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "empty.pdf", []);

        await ExtractionApi.RunJobsAsync(api, Good());

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal((ExtractionStatus.Failed, "The file is empty."), (extraction.Status, extraction.Error));
    }

    [Fact]
    public async Task A_sheet_the_service_refuses_fails_with_its_reason_and_is_not_retried()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "locked.pdf");
        var reader = new FlakySheetReader(_ => new SheetReaderException("The service refused the file.", permanent: true), SheetFixtures.Sheet());

        await ExtractionApi.RunJobsAsync(api, reader);

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal((ExtractionStatus.Failed, "The service refused the file."), (extraction.Status, extraction.Error));
        Assert.Equal((1, 1), (reader.Calls, extraction.Job.Attempts));
    }

    [Fact]
    public async Task A_transient_error_is_retried_and_the_sheet_still_reaches_review()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        var reader = new FlakySheetReader(call => call == 1 ? new HttpRequestException("503") : null, SheetFixtures.Sheet());

        await ExtractionApi.RunJobsAsync(api, reader);

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.NeedsReview, extraction.Status);
        Assert.Equal((2, 2), (reader.Calls, extraction.Job.Attempts));
    }

    [Fact]
    public async Task A_reader_that_keeps_failing_shows_the_extraction_as_failed_with_a_message_free_of_the_sheet()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        var reader = new FlakySheetReader(_ => new HttpRequestException("Vote MA/2011/042 could not be read"), SheetFixtures.Sheet());

        await ExtractionApi.RunJobsAsync(api, reader);

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.Failed, extraction.Status);
        Assert.Equal((3, JobState.Failed), (extraction.Job.Attempts, extraction.Job.State));
        Assert.DoesNotContain("MA/2011/042", extraction.Error);
        Assert.Contains("HttpRequestException", extraction.Error);
    }

    [Fact]
    public async Task A_reader_that_takes_too_long_fails_the_extraction_at_its_time_limit()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");

        await ExtractionApi.RunJobsAsync(api, new HangingSheetReader(), new Dictionary<string, string?> { ["Jobs:ExtractionTimeout"] = "00:00:00.300" });

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.Failed, extraction.Status);
        Assert.Contains("time limit", extraction.Error);
    }

    [Fact]
    public async Task A_read_that_finishes_after_the_sheet_was_read_and_edited_does_not_wipe_the_edits()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        // While this (stale) read works, another run has finished and an analyst has edited the sheet.
        var slow = new SlowSheetReader(async () =>
        {
            await using var connection = new Npgsql.NpgsqlConnection(api.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new Npgsql.NpgsqlCommand("""
                UPDATE extraction SET status = 'needs_review',
                  sheet = '{"header":{"voteId":"MA/EDITED/1","session":"66","date":"4 October 2011","title":"Edited"},"rows":[],"printed":null}'::jsonb
                WHERE id = @id
                """, connection);
            command.Parameters.AddWithValue("id", created.ExtractionId);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }, SheetFixtures.Sheet());

        await ExtractionApi.RunJobsAsync(api, slow);

        var extraction = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal(ExtractionStatus.NeedsReview, extraction.Status);
        Assert.Equal("MA/EDITED/1", extraction.Header!.VoteId);
        Assert.Empty(extraction.Rows);
    }

    [Fact]
    public async Task Retrying_a_failed_extraction_queues_it_again_and_a_good_read_then_reaches_review()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        await ExtractionApi.RunJobsAsync(api, new FlakySheetReader(_ => new SheetReaderException("The service refused the file.", true), SheetFixtures.Sheet()));

        var response = await ExtractionApi.RetryAsync(client, created.ExtractionId);

        Assert.Equal(202, (int)response.StatusCode);
        var queued = await ExtractionApi.GetAsync(client, created.ExtractionId);
        Assert.Equal((ExtractionStatus.Extracting, null, JobState.Queued, 0), (queued.Status, queued.Error, queued.Job.State, queued.Job.Attempts));
        Assert.Equal(created.JobId, queued.Job.Id);

        await ExtractionApi.RunJobsAsync(api, Good());

        Assert.Equal(ExtractionStatus.NeedsReview, (await ExtractionApi.GetAsync(client, created.ExtractionId)).Status);
    }

    [Fact]
    public async Task Retrying_an_extraction_that_has_not_failed_is_refused_with_409()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");

        await (await ExtractionApi.RetryAsync(client, created.ExtractionId)).AssertProblemAsync(409, "not-retryable");

        await ExtractionApi.RunJobsAsync(api, Good());
        await (await ExtractionApi.RetryAsync(client, created.ExtractionId)).AssertProblemAsync(409, "not-retryable");
    }

    [Fact]
    public async Task Retrying_an_unknown_extraction_is_404()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        await (await ExtractionApi.RetryAsync(api.ClientAs("dana"), 999)).AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task Two_retries_at_once_queue_it_once_and_the_other_gets_409()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs("dana");
        var created = await ExtractionApi.UploadOkAsync(client, "MA_2011_042.pdf");
        await ExtractionApi.RunJobsAsync(api, new FlakySheetReader(_ => new SheetReaderException("The service refused the file.", true), SheetFixtures.Sheet()));

        var results = await RaceBarrier.RunTogetherAsync(2, async _ =>
            (int)(await ExtractionApi.RetryAsync(api.ClientAs("dana"), created.ExtractionId)).StatusCode);

        Assert.Equal([202, 409], results.Order());
    }
}

/// <summary>Never answers until it is cancelled, as a service that hangs.</summary>
public sealed class HangingSheetReader : IVoteSheetReader
{
    public async Task<Meridian.Domain.Sheets.SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }
}
