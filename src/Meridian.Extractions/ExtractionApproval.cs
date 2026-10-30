using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;
using Meridian.Imports;
using Meridian.Platform.Identity;
using Meridian.Platform.Jobs;
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
        var (extraction, refusal) = await ExtractionDecisions.LoadForReviewAsync(db, extractionId, forUpdate: true, ct);
        if (extraction is null) return refusal!;
        var (approvable, blocked) = await ExtractionDecisions.ApprovableAsync(db, extraction, ct);
        if (approvable is null) return blocked!;

        var importId = await FileUploads.NextIdAsync(db, ct);
        var now = DateTimeOffset.UtcNow;
        var rows = approvable.Rows;
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

        await writer.WriteVoteAsync(importId, RowNormaliser.VoteKey(rows[0]), rows, approvable.Members, ct);

        var (decision, lost) = await ExtractionDecisions.DecideAsync(
            db, extractionId, user.UserId(), ExtractionStatus.Approved, request.Comment, importId, ct);
        if (decision is null) return lost!;
        var checkJson = ExtractionJson.Write(approvable.Check);
        await db.Extractions.Where(x => x.Id == extractionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CheckResult, checkJson), ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Ok(new ExtractionApprovedDto(decision, importId));
    }
}
