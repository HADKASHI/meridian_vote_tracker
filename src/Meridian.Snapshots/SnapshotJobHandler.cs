using System.Data;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Scoring;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Meridian.Snapshots;

/// <summary>
/// Calculates a snapshot (ADR 0004): in one <c>REPEATABLE READ</c> transaction it resolves
/// the selection, re-checks positions, reads the facts from the current views, scores them
/// with <see cref="AlignmentScoring"/>, and writes votes, lines, results and warnings. The
/// transaction is the whole answer, so a retry either finds it written or starts afresh.
/// </summary>
public sealed class SnapshotJobHandler(MeridianDbContext db, ILogger<SnapshotJobHandler> logger) : IJobHandler
{
    public JobKind Kind => JobKind.Snapshot;

    public async Task RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        var snapshotId = context.SnapshotId ?? throw new JobFailedException("The job has no snapshot.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        if (await db.SnapshotResults.AnyAsync(r => r.SnapshotId == snapshotId, cancellationToken))
        {
            logger.LogInformation("Snapshot {SnapshotId} was already calculated by an earlier attempt", snapshotId);
            return;
        }

        var snapshot = await db.Snapshots.AsNoTracking().SingleAsync(s => s.Id == snapshotId, cancellationToken);
        var selected = SnapshotSelection.Resolve(db, SnapshotJson.ReadSelection(snapshot.Selection));
        var votes = await selected.OrderBy(v => v.VoteId).ToListAsync(cancellationToken);
        if (votes.Count == 0)
            throw new JobFailedException("No votes match the selection any more, so there is nothing to calculate.");
        await context.ReportAsync(votesTotal: votes.Count, cancellationToken: cancellationToken);
        await RefuseVotesWithoutPositionAsync(votes, cancellationToken);

        var warnings = await SnapshotSelection.WarningsAsync(db, selected, calculating: true, cancellationToken);
        var (facts, revisions) = await ReadFactsAsync(selected, votes, cancellationToken);
        var outcome = AlignmentScoring.Score(facts);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        db.SnapshotVotes.AddRange(votes.Select(v => new SnapshotVote
        {
            SnapshotId = snapshotId, VoteId = v.VoteId,
            DetailsRevisionId = v.DetailsRevisionId, PositionRevisionId = v.PositionRevisionId!.Value,
        }));
        db.SnapshotLines.AddRange(outcome.Lines.Select(l => new SnapshotLine
        {
            SnapshotId = snapshotId, VoteId = l.VoteId, StateCode = l.StateCode, Category = l.Category,
            CountryVoteRevisionId = revisions.TryGetValue((l.VoteId, l.StateCode), out var revisionId) ? revisionId : null,
        }));
        db.SnapshotResults.AddRange(outcome.Results.Select(r => new SnapshotResult
        {
            SnapshotId = snapshotId, StateCode = r.StateCode, MatchedWeight = r.MatchedWeight,
            CountedWeight = r.CountedWeight, Rank = r.Rank,
        }));
        db.SnapshotWarnings.AddRange(warnings.Select(w => new SnapshotWarning
        {
            SnapshotId = snapshotId, Kind = SnakeCaseEnumConverter<SnapshotWarningKind>.ToName(w.Kind),
            VoteId = w.Vote?.VoteId, Detail = SnapshotJson.WriteWarning(w),
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await context.ReportAsync(votesDone: votes.Count, cancellationToken: cancellationToken);
        logger.LogInformation("Snapshot {SnapshotId} calculated over {VoteCount} votes, {LineCount} lines, {WarningCount} warnings",
            snapshotId, votes.Count, outcome.Lines.Count, warnings.Count);
    }

    /// <summary>POS-04 again, inside the transaction: an import may have added a vote after the preview.</summary>
    private async Task RefuseVotesWithoutPositionAsync(List<CurrentVote> votes, CancellationToken cancellationToken)
    {
        var missing = votes.Where(v => v.PositionRevisionId is null).Select(v => v.VoteId).ToList();
        if (missing.Count == 0) return;
        var firstSeen = await db.Votes.AsNoTracking().Where(v => missing.Contains(v.Id))
            .OrderBy(v => v.AssemblyVoteId)
            .Select(v => $"{v.AssemblyVoteId} (first seen in import #{v.FirstSeenImportId})")
            .ToListAsync(cancellationToken);
        throw new JobFailedException(
            $"{string.Join(", ", firstSeen)} {(missing.Count == 1 ? "has" : "have")} no position, so the snapshot can't be calculated. " +
            "Set the position or remove the vote, then create the snapshot again.");
    }

    /// <summary>A fact for every member state in every vote, and the country vote revision each one used.</summary>
    private async Task<(List<ScoringFact> Facts, Dictionary<(long, string), long> Revisions)> ReadFactsAsync(
        IQueryable<CurrentVote> selected, List<CurrentVote> votes, CancellationToken cancellationToken)
    {
        var states = await db.MemberStates.AsNoTracking().OrderBy(s => s.Code).Select(s => s.Code).ToListAsync(cancellationToken);
        var periods = (await db.MembershipPeriods.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(p => p.StateCode);
        var ballots = await (
                from c in db.CurrentCountryVotes
                join v in selected on c.VoteId equals v.VoteId
                select new { c.VoteId, c.StateCode, c.RevisionId, c.Ballot })
            .ToDictionaryAsync(c => (c.VoteId, c.StateCode), cancellationToken);

        var facts = new List<ScoringFact>(votes.Count * states.Count);
        foreach (var vote in votes)
        foreach (var state in states)
        {
            // Membership is inclusive at both ends; no end date means still a member.
            var member = periods[state].Any(p => p.MemberFrom <= vote.VoteDate && (p.MemberTo is null || p.MemberTo >= vote.VoteDate));
            facts.Add(new ScoringFact(vote.VoteId, state, vote.Stance!.Value, vote.Weight!.Value, member,
                ballots.TryGetValue((vote.VoteId, state), out var ballot) ? ballot.Ballot : null));
        }
        return (facts, ballots.ToDictionary(b => b.Key, b => b.Value.RevisionId));
    }
}
