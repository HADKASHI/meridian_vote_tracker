using System.Text.Json;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Meridian.Imports;

/// <summary>
/// Runs one import: parse the whole file, record the parse facts, then classify and write
/// it one vote at a time. A retry resumes from what an earlier attempt committed.
/// </summary>
internal sealed class ImportJobHandler(MeridianDbContext db, BlobContainerClient blobs, ILogger<ImportJobHandler> logger)
    : IJobHandler
{
    public JobKind Kind => JobKind.Import;

    public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        var importId = context.ImportId!.Value;
        var import = await db.Imports.AsNoTracking().SingleAsync(i => i.Id == importId, cancellationToken);

        ParsedExport parsed;
        await using (var file = await blobs.GetBlobClient(import.BlobName).OpenReadAsync(cancellationToken: cancellationToken))
            parsed = await ExportParser.ParseAsync(file,
                lines => context.ReportAsync(rowsRead: lines, cancellationToken: cancellationToken), cancellationToken);
        await context.ReportAsync(rowsRead: parsed.DataRows, cancellationToken: cancellationToken);

        await RecordParseAsync(importId, parsed, cancellationToken);
        if (parsed.RejectedReason is not null)
        {
            logger.LogInformation("Import {ImportId} rejected: {DataRows} data rows", importId, parsed.DataRows);
            return;
        }
    }

    /// <summary>The parse record and the file-level warnings, written once even when the job is retried.</summary>
    private async Task RecordParseAsync(long importId, ParsedExport parsed, CancellationToken cancellationToken)
    {
        if (await db.ImportParses.AnyAsync(p => p.ImportId == importId, cancellationToken)) return;

        db.ImportParses.Add(new ImportParse
        {
            ImportId = importId,
            Header = JsonSerializer.Serialize(parsed.Header),
            HasBom = parsed.HasBom,
            LineEndings = SnakeCaseEnumConverter<LineEnding>.ToName(parsed.LineEndings),
            DataRows = parsed.DataRows,
            MaxPublishedAt = parsed.MaxPublishedAt,
            RejectedReason = parsed.RejectedReason,
        });
        if (parsed.ExtraColumns.Count > 0)
            db.ImportWarnings.Add(new ImportWarningDetail(
                    $"Ignored column{(parsed.ExtraColumns.Count > 1 ? "s" : "")} not in the export format: {string.Join(", ", parsed.ExtraColumns)}.",
                    Columns: parsed.ExtraColumns)
                .ToWarning(importId, ImportWarningKind.ExtraColumns));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }
}
