using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.Data;

/// <summary>
/// Used by <c>dotnet ef</c> and the migration bundle, so migrating never boots the API host.
/// Reads the same keys as the apps, from environment variables only.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MeridianDbContext>
{
    private const string LocalConnection =
        "Host=localhost;Port=5433;Database=meridian;Username=meridian;Password=meridian_local";

    public MeridianDbContext CreateDbContext(string[] args) =>
        new(MeridianDb.Options(
            Environment.GetEnvironmentVariable("ConnectionStrings__Meridian") ?? LocalConnection,
            Environment.GetEnvironmentVariable("Seed__MemberStatesPath")));
}
