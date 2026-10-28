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
    /// <param name="assemblyVoteId">Null for the rows that can't name a vote; they are all invalid.</param>
    public async Task WriteVoteAsync(long importId, string? assemblyVoteId, IReadOnlyList<ExportRow> rows,
        MemberStates members, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        long? voteId = assemblyVoteId is null ? null : await LockVoteAsync(importId, assemblyVoteId, ct);
        var approved = voteId is { } id ? await ApprovedAsync(id, ct) : ApprovedVote.Nothing;
        var verdict = Classifier.Classify(rows, approved, members);
        // Rule 11, written with the vote so a resumed import never writes it twice.
        if (voteId is not null && Classifier.MissingMembers(verdict, approved, members) is { Count: > 0 } missing)
            db.ImportWarnings.Add(new ImportWarningDetail(
                    $"Incomplete vote: {missing.Count} member{(missing.Count > 1 ? "s" : "")} on its date with no approved ballot.",
                    MissingStateCodes: missing)
                .ToWarning(importId, ImportWarningKind.IncompleteVote, voteId));
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
        var conflicts = await (
                from c in db.Conflicts
                where c.VoteId == voteId
                join d in db.ConflictDecisions on c.Id equals d.ConflictId into decisions
                from d in decisions.DefaultIfEmpty()
                select new { Conflict = c, Decision = d == null ? (Decision?)null : d.Decision })
            .AsNoTracking().ToListAsync(ct);
        return new ApprovedVote(details, ballots, conflicts.Select(x => x.Conflict.Subject == ConflictSubject.Ballot
                ? new KnownConflict(x.Conflict.Id, ConflictSubject.Ballot, x.Conflict.ComparedCountryVoteRevisionId!.Value,
                    x.Conflict.StateCode!.Trim(), x.Conflict.IncomingBallot, null, x.Decision)
                : new KnownConflict(x.Conflict.Id, ConflictSubject.Details, x.Conflict.ComparedDetailsRevisionId!.Value, null, null,
                    new VoteDetails(x.Conflict.IncomingSession!.Value, x.Conflict.IncomingVoteDate!.Value, x.Conflict.IncomingTitle!),
                    x.Decision))
            .ToList());
    }

    private async Task WriteAsync(long importId, long? voteId, VoteVerdict verdict, ApprovedVote approved, CancellationToken ct)
    {
        var newBallots = verdict.Rows.Count(r => r.Ballot.Kind == LinkKind.NewRevision);
        var newBallotConflicts = verdict.Rows.Count(r => r.Ballot.Kind == LinkKind.NewConflict);
        var ids = new Queue<long>(await ReserveIdsAsync(verdict.Rows.Count + newBallots + newBallotConflicts
            + (verdict.NewDetails is null ? 0 : 1) + (verdict.NewDetailsConflict is null ? 0 : 1), ct));

        var rowIds = verdict.Rows.Select(_ => ids.Dequeue()).ToList();
        long? detailsConflictId = null;
        if (verdict.NewDetailsConflict is { } incoming)
        {
            detailsConflictId = ids.Dequeue();
            db.Conflicts.Add(new Conflict
            {
                Id = detailsConflictId.Value, VoteId = voteId!.Value, Subject = ConflictSubject.Details, OpenedByImportId = importId,
                IncomingSession = incoming.Session, IncomingVoteDate = incoming.VoteDate, IncomingTitle = incoming.Title,
                IncomingTitleKey = RowNormaliser.ComparisonKey(incoming.Title),
                ComparedDetailsRevisionId = approved.Details!.RevisionId,
            });
        }
        long? newDetailsId = null;
        if (verdict.NewDetails is { } details)
        {
            newDetailsId = ids.Dequeue();
            var origin = verdict.Rows.Select((r, i) => (r, i)).First(x => x.r.Details.Kind == LinkKind.NewRevision).i;
            db.VoteDetailsRevisions.Add(new VoteDetailsRevision
            {
                Id = newDetailsId.Value, VoteId = voteId!.Value, Session = details.Session, VoteDate = details.VoteDate,
                Title = details.Title, OriginImportRowId = rowIds[origin],
            });
            if (approved.Details is null)
                db.VoteCurrents.Add(new VoteCurrent { VoteId = voteId.Value, DetailsRevisionId = newDetailsId.Value });
        }

        for (var i = 0; i < verdict.Rows.Count; i++)
        {
            var row = verdict.Rows[i];
            long? ballotRevisionId = row.Ballot.Kind switch
            {
                LinkKind.NewRevision => ids.Dequeue(),
                LinkKind.Approved => row.Ballot.Id,
                _ => null,
            };
            if (row.Ballot.Kind == LinkKind.NewRevision)
            {
                var state = row.Values!.StateCode!;
                db.CountryVoteRevisions.Add(new CountryVoteRevision
                {
                    Id = ballotRevisionId!.Value, VoteId = voteId!.Value, StateCode = state, Ballot = row.Values.Ballot!.Value,
                    OriginImportRowId = rowIds[i],
                });
                db.CountryVoteCurrents.Add(new CountryVoteCurrent { VoteId = voteId.Value, StateCode = state, RevisionId = ballotRevisionId.Value });
            }
            if (row.Ballot.Kind == LinkKind.NewConflict)
            {
                var state = row.Values!.StateCode!;
                var conflictId = ids.Dequeue();
                db.Conflicts.Add(new Conflict
                {
                    Id = conflictId, VoteId = voteId!.Value, Subject = ConflictSubject.Ballot, OpenedByImportId = importId,
                    StateCode = state, IncomingBallot = row.Values.Ballot, ComparedCountryVoteRevisionId = approved.Ballots[state].RevisionId,
                });
                Link(rowIds[i], conflictId);
            }
            else if (row.Ballot.Kind == LinkKind.Conflict) Link(rowIds[i], row.Ballot.Id!.Value);
            if (row.Details.Kind == LinkKind.NewConflict) Link(rowIds[i], detailsConflictId!.Value);
            else if (row.Details.Kind == LinkKind.Conflict) Link(rowIds[i], row.Details.Id!.Value);

            db.ImportRows.Add(ToRecord(importId, voteId, rowIds[i], row, ballotRevisionId, row.Details.Kind switch
            {
                LinkKind.NewRevision => newDetailsId,
                LinkKind.Approved => row.Details.Id,
                _ => null,
            }));
        }
        await db.SaveChangesAsync(ct);
    }

    private void Link(long rowId, long conflictId) =>
        db.ImportRowConflicts.Add(new ImportRowConflict { ImportRowId = rowId, ConflictId = conflictId });

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
