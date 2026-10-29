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
        var extraction = await db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == extractionId, ct);
        if (extraction is null || extraction.Status != ExtractionStatus.NeedsReview)
            return ExtractionDecisions.RefusalFor(extraction, extractionId);

        var sheet = ExtractionJson.Read<MappedSheet>(extraction.Sheet)!;
        var members = await db.LoadMemberStatesAsync(ct);
        var check = SheetValidator.Check(sheet, extraction.FileName, members);
        if (!check.CanApprove) return ExtractionDecisions.BlockedBy(check);

        var rows = SheetRows.ToExportRows(sheet, check.Details!);
        var (verdict, voteExists) = await writer.ClassifyAsync(RowNormaliser.VoteKey(rows[0])!, rows, members, ct);
        int Count(Outcome outcome) => verdict.Rows.Count(r => r.Outcome == outcome);
        return TypedResults.Ok(new ExtractionPreviewDto(
            new OutcomeCountsDto(Count(Outcome.New), Count(Outcome.Unchanged), Count(Outcome.Duplicate), Count(Outcome.Invalid),
                Count(Outcome.Contradiction), Count(Outcome.Conflict)),
            voteExists,
            verdict.Rows.Where(r => r.Outcome == Outcome.Conflict).Select(r => r.Values!.StateCode!).Order(StringComparer.Ordinal).ToList()));
    }
}
