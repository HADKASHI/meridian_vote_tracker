using Meridian.Data;
using Meridian.Fixtures;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Fixtures;

public sealed class DemoSeedTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_demo_fills_an_empty_database_and_refuses_a_second_run()
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(await postgres.CreateDatabaseAsync()));

        await DemoSeed.RunAsync(db, TestContext.Current.CancellationToken);

        Assert.Equal(6, await db.CurrentVotes.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.PendingConflicts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Publications.CountAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSeed.RunAsync(db, TestContext.Current.CancellationToken));
    }
}
