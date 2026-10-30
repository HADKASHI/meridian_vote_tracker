using Meridian.Domain.Sheets;
using Meridian.Extractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meridian.Integration.Tests.Extractions;

/// <summary>
/// The one place Azure is really called (PDF-02). It runs only on demand, with <c>DOCINTEL_ENDPOINT</c> and
/// <c>DOCINTEL_KEY</c> set in the environment (the same names as in <c>.env</c>) and the scanned sheets in
/// <c>candidate_data/voting_sheets/</c>: never in CI, and it skips itself when either is missing. It asserts that
/// every sheet reads as a vote with a header, rows and printed totals, and reports how close the counts come to
/// the printed totals, as numbers only: a real read is judged by the analyst, not by this test.
/// </summary>
[Trait("Category", "RealData")]
public sealed class DocumentIntelligenceSmokeTests
{
    [Fact]
    public async Task Every_real_sheet_reads_as_a_vote_with_a_header_rows_and_printed_totals()
    {
        var endpoint = Environment.GetEnvironmentVariable("DOCINTEL_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("DOCINTEL_KEY");
        var sheets = SheetFiles();
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key) && sheets.Length > 0,
            "Set DOCINTEL_ENDPOINT and DOCINTEL_KEY and put the scanned sheets in candidate_data/voting_sheets/ to run this.");

        var reader = new DocumentIntelligenceSheetReader(new HttpClient { Timeout = TimeSpan.FromMinutes(3) },
            Options.Create(new DocumentIntelligenceOptions { Endpoint = endpoint, Key = key }), NullLogger<DocumentIntelligenceSheetReader>.Instance);

        foreach (var path in sheets)
        {
            await using var pdf = File.OpenRead(path);
            var sheet = SheetMapper.Map(await reader.ReadAsync(pdf, TestContext.Current.CancellationToken));

            var name = Path.GetFileName(path);
            Assert.True(sheet.Header.VoteId is not null && sheet.Header.Date is not null, $"{name}: the header could not be read.");
            Assert.NotNull(sheet.Printed);
            Assert.True(sheet.Rows.Count >= 100, $"{name}: only {sheet.Rows.Count} rows were read.");
            var counted = SheetValidator.Count(sheet.Rows);
            TestContext.Current.SendDiagnosticMessage(
                $"{name}: {sheet.Rows.Count} rows, {sheet.Rows.Count(r => r.Ambiguous)} with two marks; counted {counted.Total} of printed {sheet.Printed!.Total}.");
        }
    }

    private static string[] SheetFiles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Meridian.slnx")))
            directory = directory.Parent;
        var sheets = directory is null ? null : Path.Combine(directory.FullName, "candidate_data", "voting_sheets");
        return sheets is not null && Directory.Exists(sheets) ? Directory.GetFiles(sheets, "*.pdf") : [];
    }
}
