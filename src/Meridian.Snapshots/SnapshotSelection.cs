using System.Globalization;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Snapshots;

/// <summary>
/// Resolves a selection to votes over approved data, and finds what blocks it (votes
/// without a position) or only warns (pending conflicts, incomplete votes, a running
/// import). Shared by the preview, the create call and the snapshot job, so all three
/// judge a selection the same way.
/// </summary>
internal static class SnapshotSelection
{
    /// <summary>The selected votes with their approved details and position, removed votes left out.</summary>
    public static IQueryable<CurrentVote> Resolve(MeridianDbContext db, SnapshotSelectionDto selection)
    {
        var votes = db.CurrentVotes.AsNoTracking();
        if (selection.From is { } from && selection.To is { } to)
            votes = votes.Where(v => v.VoteDate >= from && v.VoteDate <= to);
        else if (selection.Sessions is { } sessions)
            votes = votes.Where(v => sessions.Contains(v.Session));
        else if (selection.VoteIds is { } voteIds)
            votes = votes.Where(v => voteIds.Contains(v.VoteId));
        else
            throw new ArgumentException("A selection needs a date range, sessions or votes.", nameof(selection));

        if (selection.ExcludedVoteIds is { Count: > 0 } excluded)
            votes = votes.Where(v => !excluded.Contains(v.VoteId));
        return votes;
    }

    /// <summary>POS-04: votes without a position block the snapshot.</summary>
    public static Task<List<VoteRefDto>> NoPositionAsync(IQueryable<CurrentVote> votes, CancellationToken ct) =>
        votes.Where(v => v.PositionRevisionId == null)
            .OrderBy(v => v.AssemblyVoteId)
            .Select(v => new VoteRefDto(v.VoteId, v.AssemblyVoteId))
            .ToListAsync(ct);

    /// <summary>
    /// What the snapshot should know but that never blocks it: a running import first,
    /// then per vote (by assembly vote id) its pending conflicts and missing member states.
    /// </summary>
    /// <param name="calculating">True inside the snapshot job, so the words say what it saw.</param>
    public static async Task<List<SnapshotWarningDto>> WarningsAsync(MeridianDbContext db, IQueryable<CurrentVote> votes,
        bool calculating, CancellationToken ct)
    {
        var warnings = new List<SnapshotWarningDto>();

        var imports = await db.Jobs.AsNoTracking()
            .Where(j => j.Kind == JobKind.Import && j.State == JobState.Running)
            .OrderBy(j => j.Id)
            .Select(j => new { ImportId = j.ImportId!.Value, j.VotesDone, j.VotesTotal })
            .ToListAsync(ct);
        foreach (var import in imports)
        {
            var progress = import.VotesTotal is { } total
                ? string.Create(CultureInfo.InvariantCulture, $"{import.VotesDone:N0} of {total:N0} votes")
                : "before its first vote";
            warnings.Add(new SnapshotWarningDto(SnapshotWarningKind.ImportRunning,
                calculating
                    ? $"Calculated while import #{import.ImportId} was applying ({progress}); it used the votes committed by then."
                    : $"Import #{import.ImportId} is applying ({progress}); a snapshot now uses the votes committed so far.",
                null, null, null, import.ImportId, import.VotesDone, import.VotesTotal));
        }

        var pending = await (
                from c in db.PendingConflicts
                join v in votes on c.VoteId equals v.VoteId
                group c by new { v.VoteId, v.AssemblyVoteId } into g
                select new { g.Key.VoteId, g.Key.AssemblyVoteId, Count = g.Count() })
            .ToListAsync(ct);

        var missing = await (
                from v in votes
                from p in db.MembershipPeriods
                where p.MemberFrom <= v.VoteDate && (p.MemberTo == null || p.MemberTo >= v.VoteDate)
                      && !db.CountryVoteCurrents.Any(c => c.VoteId == v.VoteId && c.StateCode == p.StateCode)
                select new { v.VoteId, v.AssemblyVoteId, p.StateCode })
            .ToListAsync(ct);

        var perVote = pending.Select(p => (p.AssemblyVoteId, Order: 0, Warning: new SnapshotWarningDto(
                SnapshotWarningKind.PendingConflicts,
                $"{p.AssemblyVoteId} has {Plural(p.Count, "pending conflict")}; the snapshot uses the approved values.",
                new VoteRefDto(p.VoteId, p.AssemblyVoteId), p.Count, null, null, null, null)))
            .Concat(missing.GroupBy(m => (m.VoteId, m.AssemblyVoteId)).Select(g =>
            {
                var codes = g.Select(m => m.StateCode).Distinct().Order(StringComparer.Ordinal).ToList();
                return (g.Key.AssemblyVoteId, Order: 1, Warning: new SnapshotWarningDto(
                    SnapshotWarningKind.IncompleteVote,
                    $"{g.Key.AssemblyVoteId} is incomplete: no approved country vote for {Plural(codes.Count, "member state")} on its date.",
                    new VoteRefDto(g.Key.VoteId, g.Key.AssemblyVoteId), null, codes, null, null, null));
            }))
            .OrderBy(w => w.AssemblyVoteId, StringComparer.Ordinal).ThenBy(w => w.Order)
            .Select(w => w.Warning);
        warnings.AddRange(perVote);
        return warnings;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
