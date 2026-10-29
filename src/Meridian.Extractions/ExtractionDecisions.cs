using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
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
    /// <returns>Null when the decision was applied, otherwise the problem to answer with.</returns>
    public static async Task<ProblemHttpResult?> DecideAsync(MeridianDbContext db, long id, string by, ExtractionStatus status,
        string? comment, long? importId, CancellationToken ct)
    {
        var decidedAt = DateTimeOffset.UtcNow;
        var applied = await db.Extractions.Where(x => x.Id == id && x.Status == ExtractionStatus.NeedsReview)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.DecidedBy, by)
                .SetProperty(x => x.DecidedAt, decidedAt).SetProperty(x => x.DecisionComment, comment)
                .SetProperty(x => x.ImportId, importId), ct);
        return applied == 1 ? null : RefusalFor(await db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct), id);
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
