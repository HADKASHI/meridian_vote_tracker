using Azure.Storage.Blobs;
using Meridian.AzureCheck;
using Meridian.Integration.Tests.Extractions;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.AzureCheck;

/// <summary>
/// ADR 0009: the operator's guided check. What can be proven without Azure is proven here against Azurite and the test
/// database; the rest is the operator's run. Whatever it prints holds no secret and none of a sheet's content.
/// </summary>
public sealed class LiveCheckTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Blob_passes_by_uploading_and_downloading_the_same_bytes_and_leaves_nothing_behind()
    {
        var container = $"check-{Guid.NewGuid():N}";

        var result = await LiveChecks.BlobAsync(azurite.ConnectionString, container, Ct);

        Assert.Equal(CheckStatus.Passed, result.Status);
        var blobs = new BlobContainerClient(azurite.ConnectionString, container);
        Assert.Empty(await blobs.GetBlobsAsync(cancellationToken: Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task A_check_without_its_configuration_says_it_is_not_configured()
    {
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.BlobAsync(null, "x", Ct)).Status);
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.DatabaseAsync(null, Ct)).Status);
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.DocumentIntelligenceAsync(null, "sheet.pdf", Ct)).Status);
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.ApplicationInsightsAsync(" ", Ct)).Status);
    }

    [Fact]
    public async Task The_database_check_reports_the_real_version_it_found()
    {
        var result = await LiveChecks.DatabaseAsync(await postgres.CreateDatabaseAsync(), Ct, requireTls: false);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Matches(@"PostgreSQL (17|18)\.\d+", result.Detail);
    }

    [Fact]
    public async Task A_failing_database_fails_the_check_without_printing_its_password()
    {
        var result = await LiveChecks.DatabaseAsync(
            "Host=db.example.invalid;Database=m;Username=u;Password=hunter2-live;Timeout=2", Ct);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("db.example.invalid", result.Detail);
        Assert.DoesNotContain("hunter2-live", result.Detail);
    }

    [Fact]
    public async Task The_sheet_check_reads_a_pdf_and_reports_counts_only()
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, ExtractionApi.SheetBytes, Ct);
        try
        {
            var result = await LiveChecks.DocumentIntelligenceAsync(new FakeSheetReader(SheetFixtures.Sheet()), pdf, Ct);

            Assert.Equal(CheckStatus.Passed, result.Status);
            Assert.Contains("11 grid rows", result.Detail);
            Assert.DoesNotContain("Protection of Cultural Heritage", result.Detail);
            Assert.DoesNotContain("MA/2011/042", result.Detail);
        }
        finally { File.Delete(pdf); }
    }

    [Fact]
    public async Task The_sheet_check_fails_when_the_file_is_missing_or_the_reader_refuses()
    {
        var missing = await LiveChecks.DocumentIntelligenceAsync(new FakeSheetReader(SheetFixtures.Sheet()), "/no/such/sheet.pdf", Ct);
        Assert.Equal(CheckStatus.Failed, missing.Status);

        var pdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, ExtractionApi.SheetBytes, Ct);
        try
        {
            var refused = await LiveChecks.DocumentIntelligenceAsync(
                new FlakySheetReader(_ => new Meridian.Extractions.SheetReaderException("The service refused the file.", permanent: true),
                    SheetFixtures.Sheet()), pdf, Ct);
            Assert.Equal(CheckStatus.Failed, refused.Status);
            Assert.Contains("refused", refused.Detail);
        }
        finally { File.Delete(pdf); }
    }

    [Fact]
    public async Task An_unusable_Application_Insights_string_fails_without_printing_it()
    {
        var result = await LiveChecks.ApplicationInsightsAsync("InstrumentationKey=not-a-guid-secret-value", Ct);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.DoesNotContain("not-a-guid-secret-value", result.Detail);
    }

    [Fact]
    public void The_report_names_each_check_and_passes_only_when_all_of_them_do()
    {
        CheckResult[] results =
        [
            new("Blob", CheckStatus.Passed, "round trip of 64 bytes"),
            new("Database", CheckStatus.Failed, "could not connect"),
            new("Document Intelligence", CheckStatus.NotConfigured, "no sheet given"),
        ];

        var text = Report.Render(results);

        Assert.Contains("PASS  Blob", text);
        Assert.Contains("FAIL  Database", text);
        Assert.Contains("SKIP  Document Intelligence", text);
        Assert.Equal(1, Report.ExitCode(results));
        Assert.Equal(0, Report.ExitCode([results[0]]));
    }

    [Fact]
    public void The_env_file_is_read_like_compose_reads_it_and_nothing_is_exported()
    {
        var values = DotEnv.Parse(
        [
            "# a comment", "", "WEB_PORT=8080",
            "AZURE_POSTGRES_CONNECTION_STRING=Host=h;Database=m;Password=p=q;SSL Mode=Require",
            "EMPTY=", "  SPACED = value ",
        ]);

        Assert.Equal("8080", values["WEB_PORT"]);
        Assert.Equal("Host=h;Database=m;Password=p=q;SSL Mode=Require", values["AZURE_POSTGRES_CONNECTION_STRING"]);
        Assert.Equal("", values["EMPTY"]);
        Assert.Equal("value", values["SPACED"]);
        Assert.Null(Environment.GetEnvironmentVariable("WEB_PORT"));
    }
}
