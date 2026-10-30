using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Storage.Blobs;
using Npgsql;

namespace Meridian.Platform.Hosting;

/// <summary>One service the app could not reach, in words an operator can act on and free of any secret.</summary>
public sealed record ConnectionFailure(string Service, string Message);

/// <summary>
/// Opens each connection once, the way the app will, and names the one that fails (ADR 0009). Managed
/// PostgreSQL refuses a connection without TLS, so a connection string that does not ask for it is
/// reported before the server's own, vaguer refusal. Nothing it returns holds a password or a key.
/// </summary>
public static partial class ConnectionCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async Task<IReadOnlyList<ConnectionFailure>> RunAsync(
        string? databaseConnectionString, string? blobConnectionString, bool requireTls, CancellationToken cancellationToken)
    {
        var checks = new List<Task<ConnectionFailure?>>();
        if (databaseConnectionString is not null) checks.Add(CheckDatabaseAsync(databaseConnectionString, requireTls, cancellationToken));
        if (blobConnectionString is not null) checks.Add(CheckBlobAsync(blobConnectionString, cancellationToken));
        return [.. (await Task.WhenAll(checks)).OfType<ConnectionFailure>()];
    }

    private static async Task<ConnectionFailure?> CheckDatabaseAsync(string connectionString, bool requireTls, CancellationToken cancellationToken)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            return new("database", "The database connection string is not valid. Check ConnectionStrings__Meridian.");
        }

        var where = $"{builder.Host}:{builder.Port}";
        if (requireTls && builder.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
            return new("database", $"The connection to the database at {where} does not require TLS, which Azure Database for PostgreSQL does. Add SSL Mode=Require to its connection string.");

        builder.Timeout = (int)Timeout.TotalSeconds;
        try
        {
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new("database", $"Could not connect to the database at {where}: {Reason(error, builder.Password)}");
        }
    }

    private static async Task<ConnectionFailure?> CheckBlobAsync(string connectionString, CancellationToken cancellationToken)
    {
        BlobServiceClient client;
        try
        {
            client = new BlobServiceClient(connectionString, new BlobClientOptions
            {
                Retry = { MaxRetries = 0, Mode = RetryMode.Fixed, NetworkTimeout = Timeout },
            });
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            return new("blob", "The Blob connection string is not valid. Check Blob__ConnectionString.");
        }

        try
        {
            await client.GetPropertiesAsync(cancellationToken);
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new("blob", $"Could not reach the Blob storage account '{client.AccountName}': {Reason(error, null, connectionString)}");
        }
    }

    /// <summary>The first line of the error, with any secret from the connection string taken out.</summary>
    private static string Reason(Exception error, string? password, string? connectionString = null)
    {
        var inner = error;
        while (inner.InnerException is { } deeper) inner = deeper;
        var text = (inner.Message.Split('\n')[0]).Trim();
        if (!string.IsNullOrEmpty(password)) text = text.Replace(password, "***");
        if (connectionString is not null)
            foreach (Match secret in Secret().Matches(connectionString))
                if (secret.Groups[1].Value.Length > 0) text = text.Replace(secret.Groups[1].Value, "***");
        return text;
    }

    [GeneratedRegex("(?:AccountKey|SharedAccessSignature)=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();
}
