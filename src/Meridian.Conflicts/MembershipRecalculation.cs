using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Conflicts;

/// <summary>
/// Every vote-details decision recalculates membership for its vote against the approved
/// date after the decision (map ticket 08 rule 8). Ballots from rows that were invalid only
/// because the state was not a member, and that pass now, are approved with the decision as
/// origin; the row records keep the verdict given at import. Approved ballots now outside
/// membership stay stored and simply stop counting, because scoring reads the current date.
/// </summary>
public static class MembershipRecalculation
{
    /// <summary>
    /// The importer's reason code for a row whose state was not a member on the vote date
    /// (slice 1 writes it). Being the only reason a row records, it means the row was
    /// invalid for nothing else.
    /// </summary>
    public const string NotMemberReasonCode = "not_member_on_vote_date";

    /// <summary>Membership is inclusive at both ends; an empty end means still a member.</summary>
    internal static bool IsMember(IEnumerable<MembershipPeriod> periods, DateOnly date) =>
        periods.Any(p => p.MemberFrom <= date && (p.MemberTo is null || date <= p.MemberTo));

    /// <summary>
    /// Runs inside the decision's transaction, under the vote lock. When several rows give
    /// one state different ballots, the earliest (upload order) is approved and each later
    /// different ballot opens a conflict against it, so nothing is dropped silently.
    /// </summary>
    internal static async Task RunAsync(MeridianDbContext db, long voteId, long decisionId, DateOnly voteDate,
        CancellationToken ct)
    {
        var rows = await db.ImportRows.AsNoTracking()
            .Where(r => r.VoteId == voteId && r.ReasonCode == NotMemberReasonCode
                        && r.StateCode != null && r.Ballot != null && r.CountryVoteRevisionId == null)
            .OrderBy(r => r.Id).ToListAsync(ct);
        if (rows.Count == 0) return;

        var approved = (await db.CountryVoteCurrents.Where(c => c.VoteId == voteId).Select(c => c.StateCode).ToListAsync(ct))
            .ToHashSet();
        var states = rows.Select(r => r.StateCode!).Distinct().ToList();
        var periods = (await db.MembershipPeriods.AsNoTracking().Where(p => states.Contains(p.StateCode)).ToListAsync(ct))
            .ToLookup(p => p.StateCode);

        foreach (var state in rows.GroupBy(r => r.StateCode!))
        {
            if (approved.Contains(state.Key) || !IsMember(periods[state.Key], voteDate)) continue;
            var ballots = state.Select(r => (Row: r, Ballot: ParseBallot(r.Ballot))).Where(b => b.Ballot is not null).ToList();
            if (ballots.Count == 0) continue;

            var revision = new CountryVoteRevision
            {
                VoteId = voteId, StateCode = state.Key, Ballot = ballots[0].Ballot!.Value, OriginDecisionId = decisionId,
            };
            db.CountryVoteRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            db.CountryVoteCurrents.Add(new CountryVoteCurrent { VoteId = voteId, StateCode = state.Key, RevisionId = revision.Id });

            foreach (var different in ballots.Skip(1).Where(b => b.Ballot != revision.Ballot).GroupBy(b => b.Ballot!.Value))
            {
                var conflict = new Conflict
                {
                    VoteId = voteId, Subject = ConflictSubject.Ballot, OpenedByImportId = different.First().Row.ImportId,
                    StateCode = state.Key, IncomingBallot = different.Key, ComparedCountryVoteRevisionId = revision.Id,
                };
                db.Conflicts.Add(conflict);
                await db.SaveChangesAsync(ct);
                db.ImportRowConflicts.AddRange(different.Select(b => new ImportRowConflict
                {
                    ImportRowId = b.Row.Id, ConflictId = conflict.Id,
                }));
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>What a details decision's recalculation did, read back from what it wrote.</summary>
    internal static async Task<MembershipRecalculationDto> ReadAsync(MeridianDbContext db, Conflict conflict,
        ConflictDecision decision, CancellationToken ct)
    {
        var created = await db.CountryVoteRevisions.Where(r => r.OriginDecisionId == decision.Id)
            .Select(r => new { r.Id, r.StateCode }).ToListAsync(ct);
        var createdIds = created.Select(r => r.Id).ToList();
        // A conflict the recalculation opened links rows the import judged invalid.
        var opened = await db.Conflicts
            .Where(c => c.ComparedCountryVoteRevisionId != null && createdIds.Contains(c.ComparedCountryVoteRevisionId.Value))
            .Where(c => db.ImportRowConflicts.Any(l => l.ConflictId == c.Id
                && db.ImportRows.Any(r => r.Id == l.ImportRowId && r.Outcome == Outcome.Invalid)))
            .OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);

        var noLongerCounted = new List<string>();
        if (decision.Decision == Decision.Accept)
        {
            var before = await db.VoteDetailsRevisions.SingleAsync(r => r.Id == decision.ReplacedDetailsRevisionId, ct);
            var after = conflict.IncomingVoteDate!.Value;
            var counted = await db.CountryVoteRevisions
                .Where(r => r.VoteId == conflict.VoteId && r.Id < decision.Id)
                .Select(r => r.StateCode).Distinct().ToListAsync(ct);
            var periods = (await db.MembershipPeriods.AsNoTracking().Where(p => counted.Contains(p.StateCode)).ToListAsync(ct))
                .ToLookup(p => p.StateCode);
            noLongerCounted = counted.Where(s => IsMember(periods[s], before.VoteDate) && !IsMember(periods[s], after))
                .Order(StringComparer.Ordinal).ToList();
        }

        return new MembershipRecalculationDto(
            created.Select(r => r.StateCode).Order(StringComparer.Ordinal).ToList(), noLongerCounted, opened);
    }

    /// <summary>A row's ballot, stored normalised by the importer: Y, N, A or X.</summary>
    private static Ballot? ParseBallot(string? ballot) => ballot switch
    {
        "Y" => Ballot.Y,
        "N" => Ballot.N,
        "A" => Ballot.A,
        "X" => Ballot.X,
        _ => null,
    };
}
