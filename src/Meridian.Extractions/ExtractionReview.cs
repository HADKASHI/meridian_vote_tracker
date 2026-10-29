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
        var id = extractionId;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var extraction = await db.Extractions.FromSqlInterpolated($"SELECT * FROM extraction WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (extraction is null || extraction.Status != ExtractionStatus.NeedsReview)
            return ExtractionDecisions.RefusalFor(extraction, id);

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

        return TypedResults.Ok((await ExtractionQueries.GetAsync(db, id, ct))!);
    }

    public static async Task<Results<Ok<ExtractionDecisionDto>, ProblemHttpResult>> RejectAsync(
        long extractionId, RejectExtractionRequest request, ClaimsPrincipal user, MeridianDbContext db, CancellationToken ct)
    {
        var id = extractionId;
        var refusal = await ExtractionDecisions.DecideAsync(db, id, user.UserId(), ExtractionStatus.Rejected,
            string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(), importId: null, ct);
        if (refusal is not null) return refusal;
        return TypedResults.Ok(ExtractionQueries.DecisionOf(await db.Extractions.AsNoTracking().SingleAsync(x => x.Id == id, ct))!);
    }
}
