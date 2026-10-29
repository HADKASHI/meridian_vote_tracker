using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;
using Meridian.Imports;
using Meridian.Platform.Identity;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>
/// PDF-05: approving a sheet makes an import of it. The stored PDF becomes the import's file (no copy), the checked
/// rows go through the importer's own writer, so new, unchanged and conflict are decided by the CSV's rules, and the
/// decision is recorded, all in one transaction: either the sheet is approved with its import, or nothing happened.
/// The sheet is checked once more here, because members can change after it was read.
/// </summary>
internal static class ExtractionApproval
{
    public static async Task<Results<Ok<ExtractionApprovedDto>, ProblemHttpResult>> ApproveAsync(
        long extractionId, ApproveExtractionRequest request, ClaimsPrincipal user, MeridianDbContext db,
        ImportWriter writer, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var extraction = await db.Extractions.FromSqlInterpolated($"SELECT * FROM extraction WHERE id = {extractionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (extraction is null || extraction.Status != ExtractionStatus.NeedsReview)
            return ExtractionDecisions.RefusalFor(extraction, extractionId);

        var sheet = ExtractionJson.Read<MappedSheet>(extraction.Sheet)!;
        var members = await db.LoadMemberStatesAsync(ct);
        var check = SheetValidator.Check(sheet, extraction.FileName, members);
        if (!check.CanApprove)
            return ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.HasBlockingIssues, "Issues remain",
                $"{check.Issues.Count(i => i.Severity == SheetIssueSeverity.Blocking)} blocking issue(s) must be fixed or the sheet rejected before it can be approved.");

        var importId = await FileUploads.NextIdAsync(db, ct);
        var now = DateTimeOffset.UtcNow;
        var rows = SheetRows.ToExportRows(sheet, check.Details!);
        db.Imports.Add(new Import
        {
            Id = importId, FileName = extraction.FileName, BlobName = extraction.BlobName, Sha256 = extraction.Sha256,
            SizeBytes = extraction.SizeBytes, UploadedBy = extraction.UploadedBy,
        });
        // The import is already finished, as a job that ran: every screen reads an import with its job.
        db.Jobs.Add(new Job
        {
            Kind = JobKind.Import, ImportId = importId, State = JobState.Done, RowsRead = rows.Count, VotesDone = 1, VotesTotal = 1,
            Attempts = 1, StartedAt = now, FinishedAt = now, TraceParent = JobQueue.CurrentTraceParent(),
        });
        await db.SaveChangesAsync(ct);

        await writer.WriteVoteAsync(importId, RowNormaliser.VoteKey(rows[0]), rows, members, ct);

        var refusal = await ExtractionDecisions.DecideAsync(db, extractionId, user.UserId(), ExtractionStatus.Approved,
            string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(), importId, ct);
        if (refusal is not null) return refusal;
        var json = ExtractionJson.Write(check);
        await db.Extractions.Where(x => x.Id == extractionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CheckResult, json), ct);
        await transaction.CommitAsync(ct);

        var approved = await db.Extractions.AsNoTracking().SingleAsync(x => x.Id == extractionId, ct);
        return TypedResults.Ok(new ExtractionApprovedDto(ExtractionQueries.DecisionOf(approved)!, importId));
    }
}
