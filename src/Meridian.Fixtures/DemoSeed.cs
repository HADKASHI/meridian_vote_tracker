using Meridian.Data;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Fixtures;

/// <summary>
/// A small, synthetic demo set for checking screens by hand (<c>seed-demo</c>, Development
/// only): six votes over the seeded member states, positions on four, pending and decided
/// conflicts, and a published snapshot. It refuses a database that already has votes.
/// </summary>
public static class DemoSeed
{
    public static async Task RunAsync(MeridianDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Votes.AnyAsync(cancellationToken))
            throw new InvalidOperationException("The database already has votes; seed-demo only fills an empty one.");
        var states = await db.MemberStates.OrderBy(s => s.Code).Select(s => s.Code).ToListAsync(cancellationToken);
        if (states.Count == 0)
            throw new InvalidOperationException("No member states are seeded; run the migration first.");

        var fx = new Fixtures(db);
        Ballot[] pattern = [Ballot.Y, Ballot.Y, Ballot.N, Ballot.A, Ballot.Y, Ballot.X, Ballot.N];
        var votes = new List<Data.Entities.Vote>();
        for (var n = 1; n <= 6; n++)
        {
            var vote = await fx.VoteAsync($"MA/2024/{n:000}", new DateOnly(2024, 1, 10).AddDays(n * 12),
                title: $"Demo resolution {n}");
            // Leave the last state without a ballot in vote 6, so it is an incomplete vote.
            await fx.CountryVotesAsync(vote, states.Take(n == 6 ? states.Count - 1 : states.Count)
                .Select((code, i) => (code, pattern[(i + n) % pattern.Length])).ToArray());
            votes.Add(vote);
        }

        for (var n = 0; n < 4; n++)
            await fx.PositionAsync(votes[n], n % 2 == 0 ? Stance.Yes : Stance.No, (short)(n + 2));

        var rev2 = await fx.ImportAsync(uploadedBy: "omar", fileName: "demo_rev2.csv");
        await fx.BallotConflictAsync(votes[0], states[0], Ballot.A, rev2);
        await fx.DetailsConflictAsync(votes[1], voteDate: new DateOnly(2024, 2, 4), openedBy: rev2);
        var decided = await fx.BallotConflictAsync(votes[2], states[1], Ballot.N, rev2);
        await fx.DecideAsync(decided, Decision.Accept, "dana", "Confirmed with the Assembly secretariat.");
        await fx.NotAppliedAsync(decided, Decision.Reject, "omar");

        var snapshot = await fx.SnapshotAsync("Demo 2024-Q1", votes.Take(4).ToList(), "dana");
        await fx.PublishAsync(snapshot, "2024-Q1");
    }
}
