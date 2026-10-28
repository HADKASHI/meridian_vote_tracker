using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Votes;

/// <summary>
/// A vote's timeline over the <c>vote_timeline</c> view: newest first, paged by history id,
/// which orders events exactly even when their times tie (ADR 0001).
/// </summary>
public static class VoteTimeline
{
    /// <returns>Null when there is no such vote.</returns>
    public static async Task<TimelinePageDto?> LoadAsync(MeridianDbContext db, long voteId, long? before, int pageSize,
        CancellationToken ct)
    {
        if (!await db.Votes.AnyAsync(v => v.Id == voteId, ct)) return null;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var events = db.VoteTimeline.AsNoTracking().Where(e => e.VoteId == voteId);
        if (before is { } id) events = events.Where(e => e.HistoryId < id);
        var rows = await events.OrderByDescending(e => e.HistoryId).Take(pageSize + 1).ToListAsync(ct);
        var more = rows.Count > pageSize;
        if (more) rows.RemoveAt(pageSize);

        var parsed = rows.Select(r => (Row: r, Detail: JsonDocument.Parse(r.Detail).RootElement)).ToList();
        var lookups = await Lookups.LoadAsync(db, parsed, ct);
        var items = parsed.Select(p => ToDto(p.Row, p.Detail, lookups)).ToList();
        return new TimelinePageDto(items, more ? items[^1].HistoryId : null);
    }

    private static TimelineEventDto ToDto(VoteTimelineEvent row, JsonElement d, Lookups lookups)
    {
        var e = new TimelineEventDto(row.HistoryId, KindOf(row.Kind), row.Actor, row.At,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        switch (e.Kind)
        {
            case TimelineEventKind.Import:
                var importId = d.GetProperty("importId").GetInt64();
                return e with
                {
                    ImportId = importId, FileName = d.GetProperty("fileName").GetString(),
                    Counts = CountsOf(d.GetProperty("counts")),
                };
            case TimelineEventKind.DetailsRevision:
                return e with
                {
                    To = new TimelineValueDto(null, d.GetProperty("session").GetInt32(), DateOf(d, "voteDate"),
                        d.GetProperty("title").GetString(), null, null),
                    Origin = lookups.Origins.Of(LongOf(d, "originImportRowId"), LongOf(d, "originDecisionId")),
                };
            case TimelineEventKind.CountryVoteRevision:
                return e with
                {
                    StateCode = d.GetProperty("stateCode").GetString(),
                    To = new TimelineValueDto(BallotOf(d, "ballot"), null, null, null, null, null),
                    Origin = lookups.Origins.Of(LongOf(d, "originImportRowId"), LongOf(d, "originDecisionId")),
                };
            case TimelineEventKind.PositionRevision:
                return e with
                {
                    To = new TimelineValueDto(null, null, null, null,
                        Vocabulary<Stance>(d.GetProperty("stance").GetString()!), d.GetProperty("weight").GetInt16()),
                };
            case TimelineEventKind.ConflictOpened:
                var subject = Vocabulary<ConflictSubject>(d.GetProperty("subject").GetString()!);
                var openedBy = d.GetProperty("importId").GetInt64();
                return e with
                {
                    ConflictId = d.GetProperty("conflictId").GetInt64(), Subject = subject,
                    StateCode = d.GetProperty("stateCode").GetString(),
                    ImportId = openedBy, FileName = lookups.FileNames.GetValueOrDefault(openedBy),
                    To = subject == ConflictSubject.Ballot
                        ? new TimelineValueDto(BallotOf(d, "incomingBallot"), null, null, null, null, null)
                        : new TimelineValueDto(null, d.GetProperty("incomingSession").GetInt32(), DateOf(d, "incomingVoteDate"),
                            d.GetProperty("incomingTitle").GetString(), null, null),
                };
            case TimelineEventKind.ConflictDecided:
                var decided = d.GetProperty("conflictId").GetInt64();
                return e with
                {
                    ConflictId = decided, Subject = Vocabulary<ConflictSubject>(d.GetProperty("subject").GetString()!),
                    StateCode = lookups.ConflictStates.GetValueOrDefault(decided),
                    Decision = Vocabulary<Decision>(d.GetProperty("decision").GetString()!),
                    Comment = d.GetProperty("comment").GetString(),
                };
            case TimelineEventKind.DecisionNotApplied:
                var attempted = d.GetProperty("conflictId").GetInt64();
                return e with
                {
                    ConflictId = attempted, Subject = Vocabulary<ConflictSubject>(d.GetProperty("subject").GetString()!),
                    StateCode = lookups.ConflictStates.GetValueOrDefault(attempted),
                    Decision = Vocabulary<Decision>(d.GetProperty("attempted").GetString()!),
                };
            case TimelineEventKind.Snapshot:
                return e with { SnapshotId = d.GetProperty("snapshotId").GetInt64(), SnapshotName = d.GetProperty("name").GetString() };
            case TimelineEventKind.Publication:
                var snapshotId = d.GetProperty("snapshotId").GetInt64();
                return e with
                {
                    PublicationId = d.GetProperty("publicationId").GetInt64(), SnapshotId = snapshotId,
                    SnapshotName = lookups.SnapshotNames.GetValueOrDefault(snapshotId),
                    Quarter = d.GetProperty("quarter").GetString(), Reason = d.GetProperty("reason").GetString(),
                };
            default:
                throw new InvalidOperationException($"Unknown timeline event kind {row.Kind}.");
        }
    }

    private static TimelineEventKind KindOf(string kind) => Vocabulary<TimelineEventKind>(kind);

    /// <summary>The view writes the same snake_case words the API uses.</summary>
    private static T Vocabulary<T>(string word) where T : struct, Enum => SnakeCaseEnumConverter<T>.FromName(word);

    private static long? LongOf(JsonElement d, string name) =>
        d.GetProperty(name).ValueKind == JsonValueKind.Null ? null : d.GetProperty(name).GetInt64();

    private static DateOnly DateOf(JsonElement d, string name) => DateOnly.Parse(d.GetProperty(name).GetString()!);

    /// <summary>Ballots are stored as their letter.</summary>
    private static Ballot BallotOf(JsonElement d, string name) => Enum.Parse<Ballot>(d.GetProperty(name).GetString()!.Trim());

    private static OutcomeCountsDto CountsOf(JsonElement counts)
    {
        int Count(Outcome outcome) =>
            counts.TryGetProperty(SnakeCaseEnumConverter<Outcome>.ToName(outcome), out var n) ? n.GetInt32() : 0;
        return new OutcomeCountsDto(Count(Outcome.New), Count(Outcome.Unchanged), Count(Outcome.Duplicate),
            Count(Outcome.Invalid), Count(Outcome.Contradiction), Count(Outcome.Conflict));
    }

    /// <summary>What a page's events point at, loaded once per page.</summary>
    private sealed record Lookups(
        Origins Origins, Dictionary<long, string> FileNames, Dictionary<long, string?> ConflictStates,
        Dictionary<long, string> SnapshotNames)
    {
        public static async Task<Lookups> LoadAsync(MeridianDbContext db,
            List<(VoteTimelineEvent Row, JsonElement Detail)> events, CancellationToken ct)
        {
            List<long> Ids(string kind, string property) => events.Where(e => e.Row.Kind == kind)
                .Select(e => e.Detail.GetProperty(property).GetInt64()).Distinct().ToList();

            var revisions = events.Where(e => e.Row.Kind is "details_revision" or "country_vote_revision")
                .Select(e => (LongOf(e.Detail, "originImportRowId"), LongOf(e.Detail, "originDecisionId")));
            var importIds = Ids("conflict_opened", "importId");
            var conflictIds = Ids("conflict_decided", "conflictId").Union(Ids("decision_not_applied", "conflictId")).ToList();
            var snapshotIds = Ids("publication", "snapshotId");

            return new Lookups(
                await Origins.LoadAsync(db, revisions, ct),
                await db.Imports.Where(i => importIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.FileName, ct),
                await db.Conflicts.Where(c => conflictIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.StateCode, ct),
                await db.Snapshots.Where(s => snapshotIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct));
        }
    }
}
