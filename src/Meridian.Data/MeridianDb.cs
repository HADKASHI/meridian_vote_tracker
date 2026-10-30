using Microsoft.EntityFrameworkCore;

namespace Meridian.Data;

/// <summary>How every process (migrate, api, worker, tests) configures the context.</summary>
public static class MeridianDb
{
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder, string connectionString, string? memberStatesPath = null) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            // Seeding runs after every migrate, inside EF's migration lock. Bundles call
            // the synchronous delegate; MigrateAsync calls the asynchronous one.
            .UseSeeding((context, _) => MemberStateSeeder.Seed((MeridianDbContext)context, memberStatesPath))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                MemberStateSeeder.SeedAsync((MeridianDbContext)context, memberStatesPath, cancellationToken));

    public static DbContextOptions<MeridianDbContext> Options(string connectionString, string? memberStatesPath = null)
    {
        var builder = new DbContextOptionsBuilder<MeridianDbContext>();
        Configure(builder, connectionString, memberStatesPath);
        return builder.Options;
    }
}
