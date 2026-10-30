using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Meridian.Snapshots;

/// <summary>
/// SNP-05: a quarter has one published snapshot. A first publication needs an empty
/// quarter; a replacement names the quarter's current publication and gives a reason.
/// The partial unique indexes decide a race: the loser gets the winner.
/// </summary>
internal static class PublishSnapshot
{
    public abstract record Outcome;
    public sealed record NotFound : Outcome;
    public sealed record NotCalculated : Outcome;
    public sealed record NothingToReplace(string Quarter) : Outcome;
    public sealed record QuarterPublished(PublicationDto Current) : Outcome;
    public sealed record Created(PublicationDto Publication) : Outcome;

    public static async Task<Outcome> RunAsync(MeridianDbContext db, long snapshotId, PublishSnapshotRequest request,
        string userId, CancellationToken ct)
    {
        var state = await db.Jobs.Where(j => j.SnapshotId == snapshotId).Select(j => (JobState?)j.State).SingleOrDefaultAsync(ct);
        if (state is null) return new NotFound();
        if (state != JobState.Done) return new NotCalculated();

        var quarter = request.Quarter!;
        var current = await PublicationQueries.CurrentAsync(db, quarter, ct);
        if (current is not null && current.Id != request.SupersedesPublicationId) return new QuarterPublished(current);
        if (current is null && request.SupersedesPublicationId is not null) return new NothingToReplace(quarter);

        var publication = new Publication
        {
            SnapshotId = snapshotId, Quarter = quarter, PublishedBy = userId,
            SupersedesPublicationId = request.SupersedesPublicationId, Reason = request.Reason?.Trim(),
        };
        db.Publications.Add(publication);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new QuarterPublished((await PublicationQueries.CurrentAsync(db, quarter, ct))!);
        }

        return new Created(await PublicationQueries.CreatedAsync(db, publication.Id, ct));
    }
}
