using Meridian.Data;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Database;

public sealed class SeedingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrating_loads_the_member_states_file()
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(await postgres.CreateDatabaseAsync()));

        var states = await db.MemberStates.OrderBy(s => s.Code).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(11, states.Count);
        Assert.Equal("Türkiye", states.Single(s => s.Code == "TUR").Name);

        var iceland = await db.MembershipPeriods.Where(p => p.StateCode == "ISL").OrderBy(p => p.MemberFrom)
            .Select(p => new { p.MemberFrom, p.MemberTo }).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [new { MemberFrom = new DateOnly(1990, 1, 1), MemberTo = (DateOnly?)new DateOnly(2019, 12, 31) },
             new { MemberFrom = new DateOnly(2022, 1, 1), MemberTo = (DateOnly?)null }],
            iceland);
        Assert.Equal(12, await db.MembershipPeriods.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrating_again_does_not_seed_twice()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString, PostgresFixture.MemberStatesPath));

        db.Database.Migrate();

        Assert.Equal(11, await db.MemberStates.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrating_seeds_the_three_users()
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(await postgres.CreateDatabaseAsync()));

        var users = await db.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.DisplayName, u.Role })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [new { Id = "dana", DisplayName = "Dana", Role = Meridian.Domain.Role.Analyst },
             new { Id = "lena", DisplayName = "Lena", Role = Meridian.Domain.Role.Admin },
             new { Id = "omar", DisplayName = "Omar", Role = Meridian.Domain.Role.Analyst }],
            users);
    }

    [Fact]
    public async Task Migrating_without_the_member_states_file_fails_naming_it()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no_such_folder", "member_states.csv");
        await using var db = new MeridianDbContext(MeridianDb.Options(await postgres.CreateEmptyDatabaseAsync(), missing));

        var error = Assert.ThrowsAny<Exception>(() => db.Database.Migrate());

        var message = error.GetBaseException().Message;
        Assert.Contains("member_states.csv", message);
        Assert.Contains(Path.GetDirectoryName(missing)!, message);
    }

    [Fact]
    public async Task Migrating_asynchronously_seeds_through_the_async_hook()
    {
        await using var db = new MeridianDbContext(MeridianDb.Options(await postgres.CreateEmptyDatabaseAsync(), PostgresFixture.MemberStatesPath));

        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(11, await db.MemberStates.CountAsync(TestContext.Current.CancellationToken));
    }
}
