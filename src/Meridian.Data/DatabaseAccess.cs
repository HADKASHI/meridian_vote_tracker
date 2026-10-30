using System.Collections.Concurrent;
using Azure.Core;
using Npgsql;

namespace Meridian.Data;

/// <summary>
/// How the database is reached. A connection string with a password is used as it is. An Azure Database for PostgreSQL
/// server whose connection string has no password is reached with a Microsoft Entra ID access token as the password: the
/// user name is the Entra principal (for example <c>me@contoso.com</c>), and the token is fetched from the credential and
/// renewed before it expires (ADR 0009).
/// </summary>
public static class DatabaseAccess
{
    public const string TokenScope = "https://ossrdbms-aad.database.windows.net/.default";

    private const string AzureHostSuffix = ".postgres.database.azure.com";

    // A token lives an hour or more; renewing at 45 minutes keeps a pooled connection's next login valid.
    private static readonly TimeSpan RenewAfter = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(10);

    // One data source (and so one pool and one token refresh loop) per connection string, not one per request.
    private static readonly ConcurrentDictionary<string, NpgsqlDataSource> DataSources = new();

    public static bool UsesEntra(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return string.IsNullOrEmpty(builder.Password) && !string.IsNullOrEmpty(builder.Host)
            && builder.Host.Split(',').Any(h => h.Trim().EndsWith(AzureHostSuffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The data source for <paramref name="connectionString"/>, or null when it has a password and Npgsql should use it directly.</summary>
    public static NpgsqlDataSource? SharedEntraDataSource(string connectionString) =>
        UsesEntra(connectionString)
            ? DataSources.GetOrAdd(connectionString, cs => EntraDataSource(cs, AzureCredentials.Default))
            : null;

    public static NpgsqlDataSource EntraDataSource(string connectionString, TokenCredential credential)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
            (await credential.GetTokenAsync(new TokenRequestContext([TokenScope]), cancellationToken)).Token,
            RenewAfter, RetryAfter);
        return builder.Build();
    }
}
