using Meridian.Data;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;
using Meridian.Imports;
using Meridian.Platform.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>
/// What approval would do, before it is done: the same checks, rows and classifier as the approval, run against what is
/// approved now, with nothing written and nothing locked. It can change by the time the analyst approves, which is why
/// approval classifies again.
/// </summary>
internal static class ExtractionPreview
{
    public static async Task<Results<Ok<ExtractionPreviewDto>, ProblemHttpResult>> HandleAsync(
        long extractionId, MeridianDbContext db, ImportWriter writer, CancellationToken ct)
    {
        var (extraction, refusal) = await ExtractionDecisions.LoadForReviewAsync(db, extractionId, forUpdate: false, ct);
        if (extraction is null) return refusal!;
        var (approvable, blocked) = await ExtractionDecisions.ApprovableAsync(db, extraction, ct);
        if (approvable is null) return blocked!;

        var rows = approvable.Rows;
        var (verdict, voteExists) = await writer.ClassifyAsync(RowNormaliser.VoteKey(rows[0])!, rows, approvable.Members, ct);
        int Count(Outcome outcome) => verdict.Rows.Count(r => r.Outcome == outcome);
        return TypedResults.Ok(new ExtractionPreviewDto(
            new OutcomeCountsDto(Count(Outcome.New), Count(Outcome.Unchanged), Count(Outcome.Duplicate), Count(Outcome.Invalid),
                Count(Outcome.Contradiction), Count(Outcome.Conflict)),
            voteExists,
            verdict.Rows.Where(r => r.Outcome == Outcome.Conflict).Select(r => r.Values!.StateCode!).Order(StringComparer.Ordinal).ToList()));
    }
}
