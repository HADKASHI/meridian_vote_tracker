using Azure.Storage.Blobs;
using Meridian.AzureCheck;
using Meridian.Integration.Tests.Extractions;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.AzureCheck;

/// <summary>
/// ADR 0009: the operator's guided check. What can be proven without Azure is proven here against Azurite and the test
/// database; the rest is the operator's run. Whatever it prints holds no secret and none of a sheet's content.
/// </summary>
public sealed class LiveCheckTests(AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Blob_passes_by_uploading_and_downloading_the_same_bytes_and_leaves_nothing_behind()
    {
        var container = $"check-{Guid.NewGuid():N}";

        var result = await LiveChecks.BlobAsync(azurite.ConnectionString, null, container, Ct);

        Assert.Equal(CheckStatus.Passed, result.Status);
        var blobs = new BlobContainerClient(azurite.ConnectionString, container);
        Assert.Empty(await blobs.GetBlobsAsync(cancellationToken: Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task A_check_without_its_configuration_says_it_is_not_configured()
    {
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.BlobAsync(null, " ", "x", Ct)).Status);
        Assert.Equal(CheckStatus.NotConfigured, (await LiveChecks.DocumentIntelligenceAsync(null, "sheet.pdf", Ct)).Status);
    }

    private sealed class FailingCredential : global::Azure.Core.TokenCredential
    {
        public override global::Azure.Core.AccessToken GetToken(global::Azure.Core.TokenRequestContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Please run 'az login' (tenant 00000000-secret-tenant).");

        public override ValueTask<global::Azure.Core.AccessToken> GetTokenAsync(global::Azure.Core.TokenRequestContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Please run 'az login' (tenant 00000000-secret-tenant).");
    }

    [Fact]
    public async Task Blob_by_service_address_fails_naming_the_account_and_the_Entra_sign_in_when_it_cannot_sign_in()
    {
        var result = await LiveChecks.BlobAsync(null, "https://nosuchaccount.blob.core.windows.net/", "imports", Ct, new FailingCredential());

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("nosuchaccount", result.Detail);
        Assert.Contains("Entra", result.Detail);
        Assert.DoesNotContain("secret-tenant", result.Detail);
    }


    [Fact]
    public void An_endpoint_without_a_key_still_gets_a_reader_that_signs_in_with_Entra()
    {
        Assert.NotNull(LiveChecks.CreateReader("https://name.cognitiveservices.azure.com/", null));
        Assert.Null(LiveChecks.CreateReader(null, "key"));
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
    public void The_report_names_each_check_and_passes_only_when_all_of_them_do()
    {
        CheckResult[] results =
        [
            new("Blob", CheckStatus.Passed, "round trip of 64 bytes"),
            new("Document Intelligence", CheckStatus.Failed, "could not connect"),
            new("Other", CheckStatus.NotConfigured, "no sheet given"),
        ];

        var text = Report.Render(results);

        Assert.Contains("PASS  Blob", text);
        Assert.Contains("FAIL  Document Intelligence", text);
        Assert.Contains("SKIP  Other", text);
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
