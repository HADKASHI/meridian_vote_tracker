using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Fixtures;

/// <summary>
/// Writes approved data, conflicts, positions and snapshots straight into the database,
/// exactly as the real use cases would leave them, so a caller arranges its data without
/// any other feature's code. Every approved value gets a real origin: a row of a fixture
/// import, or a conflict decision. Everything is inserted; nothing guarded is updated.
/// </summary>
public sealed class Fixtures : IAsyncDisposable
{
    private readonly MeridianDbContext _db;
    private readonly bool _ownsContext;
    private Import? _defaultImport;
    private readonly Dictionary<long, int> _nextLine = [];

    public Fixtures(MeridianDbContext db) => _db = db;

    private Fixtures(MeridianDbContext db, bool ownsContext) => (_db, _ownsContext) = (db, ownsContext);

    public static Fixtures For(string connectionString) =>
        new(new MeridianDbContext(MeridianDb.Options(connectionString)), ownsContext: true);

    public async ValueTask DisposeAsync()
    {
        if (_ownsContext) await _db.DisposeAsync();
    }

    /// <summary>A fixture import: the file every fixture row belongs to, with its uploader.</summary>
    public async Task<Import> ImportAsync(string uploadedBy = "dana", string fileName = "fixture.csv")
    {
        var import = new Import
        {
            FileName = fileName,
            BlobName = $"fixtures/{Guid.NewGuid():N}/{fileName}",
            Sha256 = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()).PadRight(64, '0'),
            SizeBytes = 0,
            UploadedBy = uploadedBy,
        };
        _db.Imports.Add(import);
        await SaveAsync();
        _db.Jobs.Add(new Job
        {
            Kind = JobKind.Import, ImportId = import.Id, State = JobState.Done,
            StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow, Attempts = 1,
        });
        await SaveAsync();
        return import;
    }

    /// <summary>A vote with approved details whose origin is a row of <paramref name="origin"/>.</summary>
    public async Task<Vote> VoteAsync(string assemblyVoteId, DateOnly voteDate, int session = 79,
        string? title = null, Import? origin = null)
    {
        origin ??= await DefaultImportAsync();
        var vote = new Vote { AssemblyVoteId = assemblyVoteId, FirstSeenImportId = origin.Id };
        _db.Votes.Add(vote);
        await SaveAsync();

        var (rowId, revisionId) = (await NextIdAsync(), await NextIdAsync());
        _db.ImportRows.Add(Row(origin, rowId, vote.Id, Outcome.New, detailsRevisionId: revisionId,
            session: session, voteDate: voteDate, title: title ?? $"Resolution {assemblyVoteId}"));
        _db.VoteDetailsRevisions.Add(new VoteDetailsRevision
        {
            Id = revisionId, VoteId = vote.Id, Session = session, VoteDate = voteDate,
            Title = title ?? $"Resolution {assemblyVoteId}", OriginImportRowId = rowId,
        });
        _db.VoteCurrents.Add(new VoteCurrent { VoteId = vote.Id, DetailsRevisionId = revisionId });
        await SaveAsync();
        return vote;
    }

    /// <summary>Approved country votes, each from its own row of <paramref name="origin"/>.</summary>
    public async Task<IReadOnlyList<CountryVoteRevision>> CountryVotesAsync(Vote vote, Import? origin = null,
        params (string StateCode, Ballot Ballot)[] ballots)
    {
        origin ??= await DefaultImportAsync();
        var revisions = new List<CountryVoteRevision>();
        foreach (var (stateCode, ballot) in ballots)
        {
            var (rowId, revisionId) = (await NextIdAsync(), await NextIdAsync());
            _db.ImportRows.Add(Row(origin, rowId, vote.Id, Outcome.New, countryVoteRevisionId: revisionId,
                stateCode: stateCode, ballot: ballot.ToString()));
            var revision = new CountryVoteRevision
            {
                Id = revisionId, VoteId = vote.Id, StateCode = stateCode, Ballot = ballot, OriginImportRowId = rowId,
            };
            _db.CountryVoteRevisions.Add(revision);
            await MovePointerAsync(vote.Id, stateCode, revisionId);
            revisions.Add(revision);
        }
        await SaveAsync();
        return revisions;
    }

    public Task<IReadOnlyList<CountryVoteRevision>> CountryVotesAsync(Vote vote, params (string StateCode, Ballot Ballot)[] ballots) =>
        CountryVotesAsync(vote, null, ballots);

    /// <summary>Sets the vote's position as the admin would: a new revision, then the pointer moves.</summary>
    public async Task<PositionRevision> PositionAsync(Vote vote, Stance stance, short weight, string setBy = "lena")
    {
        var revision = new PositionRevision { VoteId = vote.Id, Stance = stance, Weight = weight, SetBy = setBy };
        _db.PositionRevisions.Add(revision);
        await SaveAsync();
        var current = await _db.VoteCurrents.SingleAsync(c => c.VoteId == vote.Id);
        current.PositionRevisionId = revision.Id;
        await SaveAsync();
        return revision;
    }

    /// <summary>A pending ballot conflict against the state's current approved country vote, opened by a row of <paramref name="openedBy"/>.</summary>
    public async Task<Conflict> BallotConflictAsync(Vote vote, string stateCode, Ballot incoming, Import? openedBy = null)
    {
        openedBy ??= await ImportAsync(uploadedBy: "omar", fileName: "revision.csv");
        var current = await _db.CountryVoteCurrents.AsNoTracking()
            .SingleAsync(c => c.VoteId == vote.Id && c.StateCode == stateCode);
        var conflict = new Conflict
        {
            VoteId = vote.Id, Subject = ConflictSubject.Ballot, OpenedByImportId = openedBy.Id,
            StateCode = stateCode, IncomingBallot = incoming, ComparedCountryVoteRevisionId = current.RevisionId,
        };
        return await OpenAsync(conflict, Row(openedBy, await NextIdAsync(), vote.Id, Outcome.Conflict,
            stateCode: stateCode, ballot: incoming.ToString()));
    }

    /// <summary>A pending vote-details conflict against the vote's current approved details.</summary>
    public async Task<Conflict> DetailsConflictAsync(Vote vote, DateOnly? voteDate = null, int? session = null,
        string? title = null, Import? openedBy = null)
    {
        openedBy ??= await ImportAsync(uploadedBy: "omar", fileName: "revision.csv");
        var current = await _db.CurrentVotes.AsNoTracking().SingleAsync(c => c.VoteId == vote.Id);
        var incoming = (Session: session ?? current.Session, Date: voteDate ?? current.VoteDate, Title: title ?? current.Title);
        var conflict = new Conflict
        {
            VoteId = vote.Id, Subject = ConflictSubject.Details, OpenedByImportId = openedBy.Id,
            IncomingSession = incoming.Session, IncomingVoteDate = incoming.Date, IncomingTitle = incoming.Title,
            // A stand-in for the comparison key; the real rule lives in the import normaliser.
            IncomingTitleKey = incoming.Title.Trim().ToLowerInvariant(),
            ComparedDetailsRevisionId = current.DetailsRevisionId,
        };
        return await OpenAsync(conflict, Row(openedBy, await NextIdAsync(), vote.Id, Outcome.Conflict,
            session: incoming.Session, voteDate: incoming.Date, title: incoming.Title));
    }

    /// <summary>
    /// Decides a conflict as the decision use case would: the decision records the value it
    /// replaced; an accept adds a revision whose origin is the decision and moves the pointer.
    /// </summary>
    public async Task<ConflictDecision> DecideAsync(Conflict conflict, Decision decision, string decidedBy = "dana",
        string? comment = null)
    {
        var currentBallot = conflict.Subject == ConflictSubject.Ballot
            ? await _db.CountryVoteCurrents.AsNoTracking().SingleAsync(c => c.VoteId == conflict.VoteId && c.StateCode == conflict.StateCode)
            : null;
        var currentDetails = conflict.Subject == ConflictSubject.Details
            ? await _db.VoteCurrents.AsNoTracking().SingleAsync(c => c.VoteId == conflict.VoteId)
            : null;

        var record = new ConflictDecision
        {
            ConflictId = conflict.Id, Decision = decision, DecidedBy = decidedBy, Comment = comment,
            ReplacedCountryVoteRevisionId = currentBallot?.RevisionId,
            ReplacedDetailsRevisionId = currentDetails?.DetailsRevisionId,
        };
        _db.ConflictDecisions.Add(record);
        await SaveAsync();
        if (decision == Decision.Reject) return record;

        if (conflict.Subject == ConflictSubject.Ballot)
        {
            var revision = new CountryVoteRevision
            {
                VoteId = conflict.VoteId, StateCode = conflict.StateCode!, Ballot = conflict.IncomingBallot!.Value,
                OriginDecisionId = record.Id,
            };
            _db.CountryVoteRevisions.Add(revision);
            await SaveAsync();
            await MovePointerAsync(conflict.VoteId, conflict.StateCode!, revision.Id);
        }
        else
        {
            var revision = new VoteDetailsRevision
            {
                VoteId = conflict.VoteId, Session = conflict.IncomingSession!.Value,
                VoteDate = conflict.IncomingVoteDate!.Value, Title = conflict.IncomingTitle!, OriginDecisionId = record.Id,
            };
            _db.VoteDetailsRevisions.Add(revision);
            await SaveAsync();
            (await _db.VoteCurrents.SingleAsync(c => c.VoteId == conflict.VoteId)).DetailsRevisionId = revision.Id;
        }
        await SaveAsync();
        return record;
    }

    /// <summary>The losing side of a decision race, recorded as not applied.</summary>
    public async Task<DecisionAttemptNotApplied> NotAppliedAsync(Conflict conflict, Decision attempted, string attemptedBy = "omar")
    {
        var attempt = new DecisionAttemptNotApplied { ConflictId = conflict.Id, Attempted = attempted, AttemptedBy = attemptedBy };
        _db.DecisionAttemptsNotApplied.Add(attempt);
        await SaveAsync();
        return attempt;
    }

    /// <summary>
    /// A calculated snapshot over <paramref name="votes"/> (each must have a position): the
    /// exact revisions used, a categorised line per vote and member state, and results,
    /// with its job done. The categories and results follow the scoring rules in their
    /// simplest form, for arranging data only; the product's scoring lives in the snapshot code.
    /// </summary>
    public async Task<Snapshot> SnapshotAsync(string name, IReadOnlyList<Vote> votes, string createdBy = "dana",
        string? note = null)
    {
        var voteIds = votes.Select(v => v.Id).ToList();
        var snapshot = new Snapshot
        {
            Name = name, Note = note, CreatedBy = createdBy, ScoringRulesVersion = "fixture",
            Selection = JsonSerializer.Serialize(new { voteIds }),
        };
        _db.Snapshots.Add(snapshot);
        await SaveAsync();

        var current = await _db.CurrentVotes.AsNoTracking().Where(v => voteIds.Contains(v.VoteId)).ToListAsync();
        var ballots = await _db.CurrentCountryVotes.AsNoTracking().Where(c => voteIds.Contains(c.VoteId)).ToListAsync();
        var periods = await _db.MembershipPeriods.AsNoTracking().ToListAsync();
        var states = await _db.MemberStates.AsNoTracking().Select(s => s.Code).ToListAsync();
        var totals = states.ToDictionary(s => s, _ => (Matched: 0, Counted: 0));

        foreach (var vote in current)
        {
            if (vote.PositionRevisionId is null)
                throw new InvalidOperationException($"Vote {vote.AssemblyVoteId} has no position, so it can't be in a snapshot.");
            _db.SnapshotVotes.Add(new SnapshotVote
            {
                SnapshotId = snapshot.Id, VoteId = vote.VoteId,
                DetailsRevisionId = vote.DetailsRevisionId, PositionRevisionId = vote.PositionRevisionId.Value,
            });
            foreach (var state in states)
            {
                var ballot = ballots.SingleOrDefault(b => b.VoteId == vote.VoteId && b.StateCode == state);
                var member = periods.Any(p => p.StateCode == state && p.MemberFrom <= vote.VoteDate
                    && (p.MemberTo is null || p.MemberTo >= vote.VoteDate));
                var category = !member ? LineCategory.NonMember
                    : ballot is null ? LineCategory.NoData
                    : ballot.Ballot == Ballot.A ? LineCategory.Abstain
                    : ballot.Ballot == Ballot.X ? LineCategory.Absent
                    : (ballot.Ballot == Ballot.Y) == (vote.Stance == Stance.Yes) ? LineCategory.Match
                    : LineCategory.Mismatch;
                if (category is LineCategory.Match or LineCategory.Mismatch)
                    totals[state] = (totals[state].Matched + (category == LineCategory.Match ? vote.Weight!.Value : 0),
                        totals[state].Counted + vote.Weight!.Value);
                _db.SnapshotLines.Add(new SnapshotLine
                {
                    SnapshotId = snapshot.Id, VoteId = vote.VoteId, StateCode = state,
                    CountryVoteRevisionId = ballot?.RevisionId, Category = category,
                });
            }
        }

        // Competition ranking on the exact score; nothing countable means no rank.
        var scored = totals.Where(t => t.Value.Counted > 0)
            .Select(t => (State: t.Key, Score: (decimal)t.Value.Matched / t.Value.Counted)).ToList();
        foreach (var (state, (matched, counted)) in totals)
            _db.SnapshotResults.Add(new SnapshotResult
            {
                SnapshotId = snapshot.Id, StateCode = state, MatchedWeight = matched, CountedWeight = counted,
                Rank = counted == 0 ? null : 1 + scored.Count(s => s.Score > (decimal)matched / counted),
            });
        _db.Jobs.Add(new Job
        {
            Kind = JobKind.Snapshot, SnapshotId = snapshot.Id, State = JobState.Done, Attempts = 1,
            StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow,
        });
        await SaveAsync();
        return snapshot;
    }

    /// <summary>Publishes a snapshot for a reporting quarter, or replaces <paramref name="supersedes"/> with a reason.</summary>
    public async Task<Publication> PublishAsync(Snapshot snapshot, string quarter, string publishedBy = "lena",
        Publication? supersedes = null, string? reason = null)
    {
        var publication = new Publication
        {
            SnapshotId = snapshot.Id, Quarter = quarter, PublishedBy = publishedBy,
            SupersedesPublicationId = supersedes?.Id, Reason = supersedes is null ? null : reason ?? "Corrected data.",
        };
        _db.Publications.Add(publication);
        await SaveAsync();
        return publication;
    }

    private async Task<Import> DefaultImportAsync() => _defaultImport ??= await ImportAsync();

    private async Task<Conflict> OpenAsync(Conflict conflict, ImportRow row)
    {
        _db.ImportRows.Add(row);
        _db.Conflicts.Add(conflict);
        await SaveAsync();
        _db.ImportRowConflicts.Add(new ImportRowConflict { ImportRowId = row.Id, ConflictId = conflict.Id });
        await SaveAsync();
        return conflict;
    }

    private async Task MovePointerAsync(long voteId, string stateCode, long revisionId)
    {
        var pointer = _db.CountryVoteCurrents.Local.SingleOrDefault(c => c.VoteId == voteId && c.StateCode == stateCode)
            ?? await _db.CountryVoteCurrents.SingleOrDefaultAsync(c => c.VoteId == voteId && c.StateCode == stateCode);
        if (pointer is null)
            _db.CountryVoteCurrents.Add(new CountryVoteCurrent { VoteId = voteId, StateCode = stateCode, RevisionId = revisionId });
        else
            pointer.RevisionId = revisionId;
    }

    private ImportRow Row(Import import, long id, long voteId, Outcome outcome, long? countryVoteRevisionId = null,
        long? detailsRevisionId = null, string? stateCode = null, string? ballot = null, int? session = null,
        DateOnly? voteDate = null, string? title = null)
    {
        var lineNo = _nextLine[import.Id] = _nextLine.GetValueOrDefault(import.Id, 1) + 1;
        return new ImportRow
        {
            Id = id, ImportId = import.Id, LineNo = lineNo, RawText = $"fixture line {lineNo}", VoteId = voteId,
            Outcome = outcome, StateCode = stateCode, Ballot = ballot, Session = session, VoteDate = voteDate, Title = title,
            CountryVoteRevisionId = countryVoteRevisionId, DetailsRevisionId = detailsRevisionId,
        };
    }

    /// <summary>Reserves an id from the shared history sequence, so a row and the revision it created can point at each other.</summary>
    private async Task<long> NextIdAsync() =>
        await _db.Database.SqlQuery<long>($"SELECT nextval('history_seq') AS \"Value\"").SingleAsync();

    private Task SaveAsync() => _db.SaveChangesAsync();
}
