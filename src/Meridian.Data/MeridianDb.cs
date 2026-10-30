using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Meridian.Data;

/// <summary>How every process (migrate, api, worker, tests) configures the context.</summary>
public static class MeridianDb
{
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder, string connectionString, string? memberStatesPath = null) =>
        Seeded(DatabaseAccess.SharedEntraDataSource(connectionString) is { } entra
            ? builder.UseNpgsql(entra)
            : builder.UseNpgsql(connectionString), memberStatesPath);

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder, NpgsqlDataSource dataSource, string? memberStatesPath = null) =>
        Seeded(builder.UseNpgsql(dataSource), memberStatesPath);

    private static DbContextOptionsBuilder Seeded(DbContextOptionsBuilder builder, string? memberStatesPath) =>
        builder
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

    public static DbContextOptions<MeridianDbContext> Options(NpgsqlDataSource dataSource, string? memberStatesPath = null)
    {
        var builder = new DbContextOptionsBuilder<MeridianDbContext>();
        Configure(builder, dataSource, memberStatesPath);
        return builder.Options;
    }
}
