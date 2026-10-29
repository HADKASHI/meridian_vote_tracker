using System.Security.Claims;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Domain.Sheets;
using Meridian.Platform.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>PDF-04: the analyst's corrections and the rejection of a sheet waiting for review.</summary>
internal static class ExtractionReview
{
    /// <summary>
    /// Applies the edits and checks the sheet again with the same call the job used, so a fixed mark can clear a
    /// blocker. The row is locked while it changes, which also makes a decision made meanwhile win cleanly.
    /// </summary>
    public static async Task<Results<Ok<ExtractionDto>, ValidationProblem, ProblemHttpResult>> SaveEditsAsync(
        long extractionId, SaveEditsRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (extraction, refusal) = await ExtractionDecisions.LoadForReviewAsync(db, extractionId, forUpdate: true, ct);
        if (extraction is null) return refusal!;

        var edited = SheetEditor.Apply(ExtractionJson.Read<MappedSheet>(extraction.Sheet)!, request.Rows, request.Header, request.Printed);
        if (edited.Errors.Count > 0)
            return TypedResults.ValidationProblem(edited.Errors.ToDictionary(e => e.Key, e => new[] { e.Value }));

        var at = DateTimeOffset.UtcNow;
        var by = user.UserId();
        var check = SheetValidator.Check(edited.Sheet, extraction.FileName, await db.LoadMemberStatesAsync(ct));
        var history = ExtractionJson.Read<List<ExtractionEditDto>>(extraction.Edits) ?? [];
        history.AddRange(edited.Edits.Select(e => new ExtractionEditDto(by, at, e.Field, e.Position, e.Code, e.From, e.To)));

        extraction.Sheet = ExtractionJson.Write(edited.Sheet);
        extraction.CheckResult = ExtractionJson.Write(check);
        extraction.Edits = ExtractionJson.Write(history);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Ok((await ExtractionQueries.GetAsync(db, extractionId, ct))!);
    }

    public static async Task<Results<Ok<ExtractionDecisionDto>, ProblemHttpResult>> RejectAsync(
        long extractionId, RejectExtractionRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct)
    {
        var (decision, refusal) = await ExtractionDecisions.DecideAsync(
            db, extractionId, user.UserId(), ExtractionStatus.Rejected, request.Comment, importId: null, ct);
        return decision is null ? refusal! : TypedResults.Ok(decision);
    }
}
