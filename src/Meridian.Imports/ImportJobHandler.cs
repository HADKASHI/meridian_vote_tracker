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
internal sealed class ImportJobHandler(
    MeridianDbContext db, BlobContainerClient blobs, ImportWriter writer, ILogger<ImportJobHandler> logger)
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

        await RecordParseAsync(import, parsed, cancellationToken);
        if (parsed.RejectedReason is not null)
        {
            logger.LogInformation("Import {ImportId} rejected: {DataRows} data rows", importId, parsed.DataRows);
            return;
        }

        var members = await MembersAsync(cancellationToken);
        // Rows that can't name a vote (wrong field count, no vote id) are written on their own.
        var groups = parsed.Rows.GroupBy(RowNormaliser.VoteKey).ToList();
        var votes = groups.Where(g => g.Key is not null).ToList();
        await context.ReportAsync(votesDone: 0, votesTotal: votes.Count, cancellationToken: cancellationToken);
        if (groups.SingleOrDefault(g => g.Key is null) is { } unattached)
            await writer.WriteVoteAsync(importId, null, unattached.ToList(), members, cancellationToken);
        var done = 0;
        foreach (var vote in votes)
        {
            await writer.WriteVoteAsync(importId, vote.Key, vote.ToList(), members, cancellationToken);
            await context.ReportAsync(votesDone: ++done, cancellationToken: cancellationToken);
        }
        logger.LogInformation("Import {ImportId} applied {Votes} votes from {DataRows} data rows", importId, votes.Count, parsed.DataRows);
    }

    private async Task<MemberStates> MembersAsync(CancellationToken cancellationToken)
    {
        var periods = await db.MembershipPeriods.AsNoTracking().ToListAsync(cancellationToken);
        return new MemberStates(periods.GroupBy(p => p.StateCode.Trim()).ToDictionary(
            g => g.Key, g => (IReadOnlyList<MembershipSpan>)g.Select(p => new MembershipSpan(p.MemberFrom, p.MemberTo)).ToList()));
    }

    /// <summary>The parse record and the file-level warnings, written once even when the job is retried.</summary>
    private async Task RecordParseAsync(Import import, ParsedExport parsed, CancellationToken cancellationToken)
    {
        var importId = import.Id;
        if (await db.ImportParses.AnyAsync(p => p.ImportId == importId, cancellationToken)) return;

        // Rule 9: the same file again is imported normally, with a warning.
        var identical = await db.Imports.Where(i => i.Sha256 == import.Sha256 && i.Id < importId)
            .OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(cancellationToken);
        if (identical is { } earlier)
            db.ImportWarnings.Add(new ImportWarningDetail($"Identical to import #{earlier}.", IdenticalToImportId: earlier)
                .ToWarning(importId, ImportWarningKind.IdenticalFile));

        // Rule 10: published_at is never used for ordering, only warned about.
        var newest = await db.ImportParses.Where(p => p.ImportId < importId)
            .MaxAsync(p => p.MaxPublishedAt, cancellationToken);
        if (parsed.MaxPublishedAt < newest)
            db.ImportWarnings.Add(new ImportWarningDetail(
                    $"Published {parsed.MaxPublishedAt:yyyy-MM-dd HH:mm} UTC, earlier than the newest file already imported ({newest:yyyy-MM-dd HH:mm} UTC).",
                    NewestPublishedAt: newest)
                .ToWarning(importId, ImportWarningKind.OlderPublishedAt));

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
