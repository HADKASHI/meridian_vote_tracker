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
        (from r in db.ImportRows
         join i in db.Imports on r.ImportId equals i.Id
         where r.Id == importRowId
         select new OriginDto(OriginKind.ImportRow, i.Id, i.FileName, r.LineNo, r.Id, i.UploadedBy, i.RecordedAt,
             null, null, null, null))
        .SingleAsync(ct);

    public static Task<OriginDto> DecisionAsync(MeridianDbContext db, long decisionId, CancellationToken ct) =>
        db.ConflictDecisions.Where(d => d.Id == decisionId)
            .Select(d => new OriginDto(OriginKind.Decision, null, null, null, null, null, null,
                d.Id, d.ConflictId, d.DecidedBy, d.RecordedAt))
            .SingleAsync(ct);
}
