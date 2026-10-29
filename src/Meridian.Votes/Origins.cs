using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Votes;

/// <summary>
/// Resolves where approved values came from: the file row that brought each one in, or the
/// conflict decision that approved it. Loaded in two queries for any number of values.
/// </summary>
public sealed class Origins
{
    private readonly Dictionary<long, OriginDto> _rows;
    private readonly Dictionary<long, OriginDto> _decisions;

    private Origins(Dictionary<long, OriginDto> rows, Dictionary<long, OriginDto> decisions) =>
        (_rows, _decisions) = (rows, decisions);

    public static async Task<Origins> LoadAsync(MeridianDbContext db, IEnumerable<(long? ImportRowId, long? DecisionId)> origins,
        CancellationToken ct)
    {
        var list = origins.ToList();
        var rowIds = list.Where(o => o.ImportRowId is not null).Select(o => o.ImportRowId!.Value).Distinct().ToList();
        var decisionIds = list.Where(o => o.DecisionId is not null).Select(o => o.DecisionId!.Value).Distinct().ToList();

        var rows = rowIds.Count == 0 ? [] : await (
                from r in db.ImportRows
                join i in db.Imports on r.ImportId equals i.Id
                where rowIds.Contains(r.Id)
                select new OriginDto(OriginKind.ImportRow, i.Id, i.FileName, r.LineNo, r.Id, i.UploadedBy, i.RecordedAt,
                    null, null, null, null))
            .AsNoTracking().ToDictionaryAsync(o => o.ImportRowId!.Value, ct);
        var decisions = decisionIds.Count == 0 ? [] : await db.ConflictDecisions
            .Where(d => decisionIds.Contains(d.Id))
            .Select(d => new OriginDto(OriginKind.Decision, null, null, null, null, null, null,
                d.Id, d.ConflictId, d.DecidedBy, d.RecordedAt))
            .AsNoTracking().ToDictionaryAsync(o => o.DecisionId!.Value, ct);
        return new Origins(rows, decisions);
    }

    /// <summary>Every revision has exactly one origin (ADR 0001), so one of the two ids is set.</summary>
    public OriginDto Of(long? importRowId, long? decisionId) =>
        importRowId is { } rowId ? _rows[rowId]
        : decisionId is { } id ? _decisions[id]
        : throw new InvalidOperationException("An approved value without an origin.");
}
