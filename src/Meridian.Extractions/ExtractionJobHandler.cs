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

        if (extraction.SizeBytes == 0)
        {
            await FailAsync(extraction, "The file is empty.", cancellationToken);
            return;
        }

        SheetRead read;
        try
        {
            await using var pdf = await blobs.GetBlobClient(extraction.BlobName).OpenReadAsync(cancellationToken: cancellationToken);
            if (!await IsPdfAsync(pdf, cancellationToken))
            {
                await FailAsync(extraction, "The file is not a PDF.", cancellationToken);
                return;
            }
            read = await reader.ReadAsync(pdf, cancellationToken);
        }
        catch (SheetReaderException error) when (error.Permanent)
        {
            // Trying again can't help: the sheet is failed now, and this job's work is finished.
            await FailAsync(extraction, error.Message, cancellationToken);
            return;
        }
        catch (SheetReaderException error)
        {
            // Worth another attempt; only a JobFailedException's message is kept on the job.
            throw new JobFailedException(error.Message);
        }

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

    private async Task FailAsync(Data.Entities.Extraction extraction, string error, CancellationToken cancellationToken)
    {
        extraction.Status = ExtractionStatus.Failed;
        extraction.Error = error;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Extraction {ExtractionId} failed: {Error}", extraction.Id, error);
    }

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    /// <summary>Every PDF starts with <c>%PDF-</c>; reads the start and rewinds the stream.</summary>
    private static async Task<bool> IsPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        var start = new byte[PdfMagic.Length];
        var read = await stream.ReadAtLeastAsync(start, start.Length, throwOnEndOfStream: false, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return read == start.Length && start.AsSpan().SequenceEqual(PdfMagic);
    }
}
