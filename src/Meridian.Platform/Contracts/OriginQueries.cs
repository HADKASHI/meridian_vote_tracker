using Meridian.Data;

namespace Meridian.Platform.Contracts;

/// <summary>The one projection of a value's origin onto <see cref="OriginDto"/>, shared by every module.</summary>
public static class OriginQueries
{
    /// <summary>The file row that brought a value in, for the given import row ids.</summary>
    public static IQueryable<OriginDto> ImportRowOrigins(this MeridianDbContext db, IReadOnlyCollection<long> importRowIds) =>
        from r in db.ImportRows
        join i in db.Imports on r.ImportId equals i.Id
        where importRowIds.Contains(r.Id)
        select new OriginDto(OriginKind.ImportRow, i.Id, i.FileName, r.LineNo, r.Id, i.UploadedBy, i.RecordedAt,
            null, null, null, null);

    /// <summary>The conflict decision that approved a value, for the given decision ids.</summary>
    public static IQueryable<OriginDto> DecisionOrigins(this MeridianDbContext db, IReadOnlyCollection<long> decisionIds) =>
        db.ConflictDecisions
            .Where(d => decisionIds.Contains(d.Id))
            .Select(d => new OriginDto(OriginKind.Decision, null, null, null, null, null, null,
                d.Id, d.ConflictId, d.DecidedBy, d.RecordedAt));
}
