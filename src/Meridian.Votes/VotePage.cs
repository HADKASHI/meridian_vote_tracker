using Meridian.Data;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Votes;

/// <summary>A vote's current state: approved details, position, country votes, each value with its origin.</summary>
public static class VotePage
{
    /// <returns>Null when there is no such vote.</returns>
    public static async Task<VoteDto?> LoadAsync(MeridianDbContext db, long voteId, CancellationToken ct)
    {
        var vote = await db.Votes.AsNoTracking().Where(v => v.Id == voteId)
            .Select(v => new { v.Id, v.AssemblyVoteId }).SingleOrDefaultAsync(ct);
        if (vote is null) return null;

        var current = await (
                from v in db.CurrentVotes
                where v.VoteId == voteId
                join d in db.VoteDetailsRevisions on v.DetailsRevisionId equals d.Id
                join p in db.PositionRevisions on v.PositionRevisionId equals p.Id into positions
                from p in positions.DefaultIfEmpty()
                select new { Details = d, Position = p })
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var ballots = await db.CurrentCountryVotes.AsNoTracking().Where(c => c.VoteId == voteId).ToListAsync(ct);
        var pending = await db.PendingConflicts.CountAsync(c => c.VoteId == voteId, ct);

        var origins = await Origins.LoadAsync(db,
            ballots.Select(b => (b.OriginImportRowId, b.OriginDecisionId))
                .Append((current?.Details.OriginImportRowId, current?.Details.OriginDecisionId)), ct);

        VoteDetailsDto? details = current?.Details is { } approved
            ? new VoteDetailsDto(approved.Id, approved.Session, approved.VoteDate, approved.Title,
                origins.Of(approved.OriginImportRowId, approved.OriginDecisionId))
            : null;
        PositionDto? position = current?.Position is { } set
            ? new PositionDto(set.Id, set.Stance, set.Weight, set.SetBy, set.RecordedAt)
            : null;

        return new VoteDto(vote.Id, vote.AssemblyVoteId, details, position,
            await CountryVotesAsync(db, details?.VoteDate, ballots, origins, ct), pending);
    }

    /// <summary>
    /// Every state that was a member on the vote date, plus any state with an approved ballot,
    /// by code. With no approved date, only the states with a ballot.
    /// </summary>
    private static async Task<List<CountryVoteDto>> CountryVotesAsync(MeridianDbContext db, DateOnly? voteDate,
        List<Data.Entities.CurrentCountryVote> ballots, Origins origins, CancellationToken ct)
    {
        var withBallot = ballots.Select(b => b.StateCode).ToList();
        var members = voteDate is { } date
            ? await db.MembershipPeriods.AsNoTracking()
                .Where(m => m.MemberFrom <= date && (m.MemberTo == null || m.MemberTo >= date))
                .Select(m => m.StateCode).Distinct().ToListAsync(ct)
            : [];
        var states = await db.MemberStates.AsNoTracking()
            .Where(s => members.Contains(s.Code) || withBallot.Contains(s.Code))
            .OrderBy(s => s.Code).ToListAsync(ct);

        return states.Select(s =>
        {
            var ballot = ballots.SingleOrDefault(b => b.StateCode == s.Code);
            return new CountryVoteDto(s.Code, s.Name, ballot?.Ballot, ballot?.RevisionId,
                ballot is null ? null : origins.Of(ballot.OriginImportRowId, ballot.OriginDecisionId),
                members.Contains(s.Code));
        }).ToList();
    }
}
