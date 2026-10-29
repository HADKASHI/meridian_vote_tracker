using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Meridian.Domain.Sheets;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Extractions;

/// <summary>
/// How an extraction is decided, for rejecting and approving alike: exactly one decision applies. The update is
/// conditional on the extraction still waiting for review, so of two simultaneous decisions one wins and the other
/// is told who did.
/// </summary>
internal static class ExtractionDecisions
{
    /// <summary>The extraction, when it is waiting for review; otherwise why it is not. Locks its row when the caller will change it.</summary>
    public static async Task<(Extraction? Extraction, ProblemHttpResult? Refusal)> LoadForReviewAsync(
        MeridianDbContext db, long id, bool forUpdate, CancellationToken ct)
    {
        var extraction = forUpdate
            ? await db.Extractions.FromSqlInterpolated($"SELECT * FROM extraction WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return extraction is { Status: ExtractionStatus.NeedsReview } ? (extraction, null) : (null, RefusalFor(extraction, id));
    }

    /// <summary>The sheet as it is stored, with the rows an import would take, or the 409 that an issue blocking approval gives.</summary>
    public sealed record Approvable(MappedSheet Sheet, MemberStates Members, SheetCheck Check, IReadOnlyList<ExportRow> Rows);

    /// <summary>
    /// What approval and its preview share: read the stored sheet, check it against the members as they are now, and
    /// turn it into the importer's rows, unless something blocks.
    /// </summary>
    public static async Task<(Approvable? Approvable, ProblemHttpResult? Blocked)> ApprovableAsync(
        MeridianDbContext db, Extraction extraction, CancellationToken ct)
    {
        var sheet = ExtractionJson.Read<MappedSheet>(extraction.Sheet)!;
        var members = await db.LoadMemberStatesAsync(ct);
        var check = SheetValidator.Check(sheet, extraction.FileName, members);
        return check.CanApprove
            ? (new Approvable(sheet, members, check, SheetRows.ToExportRows(sheet, check.Details!)), null)
            : (null, BlockedBy(check));
    }

    /// <returns>The decision when it was applied, otherwise the problem to answer with.</returns>
    public static async Task<(ExtractionDecisionDto? Decision, ProblemHttpResult? Refusal)> DecideAsync(
        MeridianDbContext db, long id, string by, ExtractionStatus status, string? comment, long? importId, CancellationToken ct)
    {
        var decidedAt = DateTimeOffset.UtcNow;
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var applied = await db.Extractions.Where(x => x.Id == id && x.Status == ExtractionStatus.NeedsReview)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.DecidedBy, by)
                .SetProperty(x => x.DecidedAt, decidedAt).SetProperty(x => x.DecisionComment, comment)
                .SetProperty(x => x.ImportId, importId), ct);
        return applied == 1
            ? (new ExtractionDecisionDto(status, by, decidedAt, comment), null)
            : (null, RefusalFor(await db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct), id));
    }

    /// <summary>Why this extraction can't be edited or decided now: unknown, already decided (with the winner) or not yet read.</summary>
    public static ProblemHttpResult RefusalFor(Extraction? x, long id) => x switch
    {
        null => ProblemDetailsSetup.Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, "Extraction not found",
            $"There is no extraction #{id}."),
        { DecidedBy: not null } => AlreadyDecided(x),
        _ => ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.NotReviewable, "Not ready for review",
            "Only a sheet that has been read and is waiting for review can be edited or decided."),
    };

    /// <summary>The 409 for a sheet that still has issues that block approval (and so its preview).</summary>
    public static ProblemHttpResult BlockedBy(SheetCheck check) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.HasBlockingIssues, "Issues remain",
            $"{check.Issues.Count(i => i.Severity == SheetIssueSeverity.Blocking)} blocking issue(s) must be fixed or the sheet rejected before it can be approved.");

    private static ProblemHttpResult AlreadyDecided(Extraction x) =>
        ProblemDetailsSetup.Problem(StatusCodes.Status409Conflict, ProblemTypes.AlreadyDecided, "Already decided",
            $"{x.DecidedBy} decided this extraction first. Your change was not applied.",
            new Dictionary<string, object?> { ["decision"] = ExtractionQueries.DecisionOf(x) });
}
