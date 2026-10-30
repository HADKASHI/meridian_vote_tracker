using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Conflicts;

/// <summary>Where a value came from: the file row that brought it, or the decision that approved it.</summary>
internal static class Origins
{
    public static Task<OriginDto> OfAsync(MeridianDbContext db, long? importRowId, long? decisionId, CancellationToken ct) =>
        importRowId is { } rowId ? ImportRowAsync(db, rowId, ct) : DecisionAsync(db, decisionId!.Value, ct);

    public static Task<OriginDto> ImportRowAsync(MeridianDbContext db, long importRowId, CancellationToken ct) =>
        db.ImportRowOrigins([importRowId]).SingleAsync(ct);

    public static Task<OriginDto> DecisionAsync(MeridianDbContext db, long decisionId, CancellationToken ct) =>
        db.DecisionOrigins([decisionId]).SingleAsync(ct);
}
