using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Meridian.Extractions;

/// <summary>
/// Reads one stored voting sheet: the reader says what it saw, the mapper reads that as a vote and the
/// validator checks it against the printed totals and the members. The sheet goes to review whatever it
/// finds; only a person approves it. Logs hold ids and counts, never the sheet's content.
/// </summary>
internal sealed class ExtractionJobHandler(
    MeridianDbContext db, BlobContainerClient blobs, IVoteSheetReader reader, ILogger<ExtractionJobHandler> logger)
    : IJobHandler
{
    public JobKind Kind => JobKind.PdfExtraction;

    public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        var id = context.ExtractionId!.Value;
        var extraction = await db.Extractions.SingleAsync(x => x.Id == id, cancellationToken);
        // A retry after the result was saved has nothing left to do.
        if (extraction.Status != ExtractionStatus.Extracting) return;

        SheetRead read;
        await using (var pdf = await blobs.GetBlobClient(extraction.BlobName).OpenReadAsync(cancellationToken: cancellationToken))
            read = await reader.ReadAsync(pdf, cancellationToken);

        var mapped = SheetMapper.Map(read);
        var check = SheetValidator.Check(mapped, extraction.FileName, await db.LoadMemberStatesAsync(cancellationToken));

        extraction.Extracted = ExtractionJson.Write(mapped);
        extraction.Sheet = extraction.Extracted;
        extraction.CheckResult = ExtractionJson.Write(check);
        extraction.Status = ExtractionStatus.NeedsReview;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Extraction {ExtractionId} read {Rows} rows with {Blocking} blocking issues and {Warnings} warnings",
            id, mapped.Rows.Count,
            check.Issues.Count(i => i.Severity == SheetIssueSeverity.Blocking),
            check.Issues.Count(i => i.Severity == SheetIssueSeverity.Warning));
    }
}
