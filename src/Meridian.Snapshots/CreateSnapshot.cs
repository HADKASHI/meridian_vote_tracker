using System.Security.Claims;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Data.Migrations;
using Meridian.Domain;
using Meridian.Domain.Scoring;
using Meridian.Platform.Contracts;
using Meridian.Platform.Identity;
using Meridian.Platform.Jobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Meridian.Snapshots;

/// <summary>
/// Creates a snapshot and its calculation job in one transaction: the admin-only rule set
/// check, the unique name, the selection with its no-vote and no-position refusals. The
/// endpoint only maps the outcome to HTTP.
/// </summary>
internal static class CreateSnapshot
{
    public abstract record Outcome;
    public sealed record Forbidden : Outcome;
    public sealed record NameTaken(string Name) : Outcome;
    public sealed record NoVotesMatch : Outcome;
    public sealed record NoPosition(IReadOnlyList<VoteRefDto> Votes) : Outcome;
    public sealed record Created(long SnapshotId, long JobId) : Outcome;

    public static async Task<Outcome> RunAsync(MeridianDbContext db, CreateSnapshotRequest request, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var selection = request.Selection!;
        var ruleSet = request.RuleSet ?? RuleSets.DefaultId;
        if (ruleSet != RuleSets.DefaultId && !user.IsInRole(SnakeCaseEnumConverter<Role>.ToName(Role.Admin)))
            return new Forbidden();
        var name = request.Name!.Trim();
        if (await SameNameAsync(db, name, ct) is { } taken) return new NameTaken(taken);
        var votes = SnapshotSelection.Resolve(db, selection);
        if (!await votes.AnyAsync(ct)) return new NoVotesMatch();
        var noPosition = await SnapshotSelection.NoPositionAsync(votes, ct);
        if (noPosition.Count > 0) return new NoPosition(noPosition);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var snapshot = new Snapshot
        {
            Name = name, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Selection = SnapshotJson.WriteSelection(selection), ScoringRulesVersion = ruleSet,
            CreatedBy = user.UserId(),
        };
        db.Snapshots.Add(snapshot);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: UniqueSnapshotNames.IndexName })
        {
            // Another create took the name between the check and this insert.
            return new NameTaken(await SameNameAsync(db, name, ct) ?? name);
        }
        var job = JobQueue.ForSnapshot(snapshot.Id);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new Created(snapshot.Id, job.Id);
    }

    /// <summary>
    /// The existing snapshot's name that <paramref name="name"/> clashes with: every snapshot
    /// holds its name, failed ones included, and case or surrounding spaces don't make a new one.
    /// </summary>
    private static Task<string?> SameNameAsync(MeridianDbContext db, string name, CancellationToken ct)
    {
        var key = name.ToLowerInvariant();
        return db.Snapshots.Where(s => s.Name.Trim().ToLower() == key).Select(s => s.Name).FirstOrDefaultAsync(ct);
    }
}
