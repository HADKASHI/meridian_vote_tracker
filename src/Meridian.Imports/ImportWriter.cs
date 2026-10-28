using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Imports;

/// <summary>
/// The bulk path's one seam (ADR 0005): writes an import one vote per transaction. It
/// takes the vote's row lock first, reads what is approved for the vote under that lock,
/// classifies the rows with the pure <see cref="Classifier"/>, and inserts the verdicts and
/// what they change. Built with EF; only its insides change if the budget is missed.
/// </summary>
internal sealed class ImportWriter(MeridianDbContext db)
{
    public async Task WriteVoteAsync(long importId, string assemblyVoteId, IReadOnlyList<ExportRow> rows,
        MemberStates members, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var voteId = await LockVoteAsync(importId, assemblyVoteId, ct);
        var approved = await ApprovedAsync(voteId, ct);
        var verdict = Classifier.Classify(rows, approved, members);
        await WriteAsync(importId, voteId, verdict, approved, ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Creates the vote the first time its id is seen, then locks it: the per-vote lock.</summary>
    private async Task<long> LockVoteAsync(long importId, string assemblyVoteId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO vote (assembly_vote_id, first_seen_import_id) VALUES ({assemblyVoteId}, {importId})
            ON CONFLICT (assembly_vote_id) DO NOTHING
            """, ct);
        var ids = await db.Database.SqlQuery<long>(
            $"SELECT id AS \"Value\" FROM vote WHERE assembly_vote_id = {assemblyVoteId} FOR UPDATE").ToListAsync(ct);
        return ids.Single();
    }

    private async Task<ApprovedVote> ApprovedAsync(long voteId, CancellationToken ct)
    {
        var details = await db.CurrentVotes.AsNoTracking().Where(v => v.VoteId == voteId)
            .Select(v => new Approved<VoteDetails>(v.DetailsRevisionId, new VoteDetails(v.Session, v.VoteDate, v.Title)))
            .SingleOrDefaultAsync(ct);
        var ballots = await db.CurrentCountryVotes.AsNoTracking().Where(c => c.VoteId == voteId)
            .ToDictionaryAsync(c => c.StateCode.Trim(), c => new Approved<Ballot>(c.RevisionId, c.Ballot), ct);
        return new ApprovedVote(details, ballots);
    }

    private async Task WriteAsync(long importId, long voteId, VoteVerdict verdict, ApprovedVote approved, CancellationToken ct)
    {
        var newBallots = verdict.Rows.Count(r => r.Ballot.Kind == LinkKind.NewRevision);
        var ids = new Queue<long>(await ReserveIdsAsync(verdict.Rows.Count + newBallots + (verdict.NewDetails is null ? 0 : 1), ct));

        var rowIds = verdict.Rows.Select(_ => ids.Dequeue()).ToList();
        long? newDetailsId = null;
        if (verdict.NewDetails is { } details)
        {
            newDetailsId = ids.Dequeue();
            var origin = verdict.Rows.Select((r, i) => (r, i)).First(x => x.r.Details.Kind == LinkKind.NewRevision).i;
            db.VoteDetailsRevisions.Add(new VoteDetailsRevision
            {
                Id = newDetailsId.Value, VoteId = voteId, Session = details.Session, VoteDate = details.VoteDate,
                Title = details.Title, OriginImportRowId = rowIds[origin],
            });
            if (approved.Details is null)
                db.VoteCurrents.Add(new VoteCurrent { VoteId = voteId, DetailsRevisionId = newDetailsId.Value });
        }

        for (var i = 0; i < verdict.Rows.Count; i++)
        {
            var row = verdict.Rows[i];
            long? ballotRevisionId = row.Ballot.Kind switch
            {
                LinkKind.NewRevision => ids.Dequeue(),
                LinkKind.Approved => row.Ballot.RevisionId,
                _ => null,
            };
            if (row.Ballot.Kind == LinkKind.NewRevision)
            {
                var state = row.Values!.StateCode!;
                db.CountryVoteRevisions.Add(new CountryVoteRevision
                {
                    Id = ballotRevisionId!.Value, VoteId = voteId, StateCode = state, Ballot = row.Values.Ballot!.Value,
                    OriginImportRowId = rowIds[i],
                });
                db.CountryVoteCurrents.Add(new CountryVoteCurrent { VoteId = voteId, StateCode = state, RevisionId = ballotRevisionId.Value });
            }
            db.ImportRows.Add(ToRecord(importId, voteId, rowIds[i], row, ballotRevisionId, row.Details.Kind switch
            {
                LinkKind.NewRevision => newDetailsId,
                LinkKind.Approved => row.Details.RevisionId,
                _ => null,
            }));
        }
        await db.SaveChangesAsync(ct);
    }

    private static ImportRow ToRecord(long importId, long? voteId, long id, RowVerdict row, long? ballotRevisionId, long? detailsRevisionId) => new()
    {
        Id = id, ImportId = importId, LineNo = row.LineNo, RawText = row.RawText, VoteId = voteId,
        StateCode = row.Values?.StateCode, CountryName = row.Values?.CountryName, Ballot = row.Values?.BallotText,
        Session = row.Values?.Session, VoteDate = row.Values?.VoteDate, Title = row.Values?.Title,
        PublishedAt = row.Values?.PublishedAt,
        Outcome = row.Outcome, ReasonCode = SnakeCaseEnumConverter<ReasonCode>.ToName(row.Reason), ReasonMessage = row.Message,
        Normalisations = row.Normalisations.Count == 0 ? null : JsonSerializer.Serialize(row.Normalisations),
        CountryVoteRevisionId = ballotRevisionId, DetailsRevisionId = detailsRevisionId,
    };

    /// <summary>Ids from <c>history_seq</c>, reserved first because a row and its revision point at each other.</summary>
    private async Task<List<long>> ReserveIdsAsync(int count, CancellationToken ct) =>
        count == 0 ? [] : await db.Database.SqlQuery<long>(
            $"SELECT nextval('history_seq') AS \"Value\" FROM generate_series(1, {count})").ToListAsync(ct);
}
