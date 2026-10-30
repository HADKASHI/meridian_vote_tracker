using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Storage.Blobs;
using Meridian.Data;
using Meridian.Platform.Storage;
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

    /// <summary>
    /// Wraps the sign-in so a failure says only that no token could be had, and which kind of error it was: the
    /// credential's own message can quote tenant and account details that do not belong in a report.
    /// </summary>
    private sealed class QuietCredential(TokenCredential inner) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            try { return inner.GetToken(requestContext, cancellationToken); }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested) { throw Quiet(error); }
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            try { return await inner.GetTokenAsync(requestContext, cancellationToken); }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested) { throw Quiet(error); }
        }

        private static InvalidOperationException Quiet(Exception error) =>
            new($"no Microsoft Entra ID token could be obtained ({error.GetType().Name}).");
    }

    private const string EntraHint = "It signs in with Microsoft Entra ID; run 'az login' (or give the process a service principal or managed identity) and check the account has access.";

    /// <param name="blobServiceUri">The Blob service address; with it, Blob is reached with the Entra sign-in instead of a key.</param>
    /// <param name="credential">The Entra sign-in to use for Blob; the process's own when null.</param>
    /// <param name="checkBlob">Report Blob as failed when no Blob setting exists at all, instead of skipping it.</param>
    public static async Task<IReadOnlyList<ConnectionFailure>> RunAsync(
        string? databaseConnectionString, string? blobConnectionString, bool requireTls, CancellationToken cancellationToken,
        string? blobServiceUri = null, TokenCredential? credential = null, bool checkBlob = false)
    {
        var checks = new List<Task<ConnectionFailure?>>();
        if (!string.IsNullOrWhiteSpace(databaseConnectionString))
            checks.Add(CheckDatabaseAsync(databaseConnectionString, requireTls, cancellationToken));
        if (checkBlob || !string.IsNullOrWhiteSpace(blobConnectionString) || !string.IsNullOrWhiteSpace(blobServiceUri))
            checks.Add(CheckBlobAsync(blobConnectionString, blobServiceUri, credential, cancellationToken));
        return [.. (await Task.WhenAll(checks)).OfType<ConnectionFailure>()];
    }

    private static async Task<ConnectionFailure?> CheckDatabaseAsync(
        string connectionString, bool requireTls, CancellationToken cancellationToken)
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
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            return new("database", $"Could not connect to the database at {where}: {Reason(error, builder.Password)}");
        }
    }

    private static async Task<ConnectionFailure?> CheckBlobAsync(
        string? connectionString, string? serviceUri, TokenCredential? credential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(serviceUri))
            return new("blob", "No Blob storage is configured. Set Blob__ServiceUri (an Entra ID sign-in) or Blob__ConnectionString.");

        var entra = !string.IsNullOrWhiteSpace(serviceUri);
        BlobServiceClient client;
        try
        {
            client = BlobAccess.Service(connectionString, serviceUri, new QuietCredential(credential ?? AzureCredentials.Default), new BlobClientOptions
            {
                Retry = { MaxRetries = 0, Mode = RetryMode.Fixed, NetworkTimeout = Timeout },
            });
        }
        catch (Exception error) when (error is ArgumentException or FormatException or UriFormatException)
        {
            return new("blob", entra
                ? "The Blob service address is not valid. Check Blob__ServiceUri."
                : "The Blob connection string is not valid. Check Blob__ConnectionString.");
        }

        try
        {
            // Listing one page of containers needs only a data role, with a key or with an Entra sign-in.
            await client.GetBlobContainersAsync(cancellationToken: cancellationToken).AsPages(pageSizeHint: 1)
                .GetAsyncEnumerator(cancellationToken).MoveNextAsync();
            return null;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            return new("blob", $"Could not reach the Blob storage account '{client.AccountName}': " +
                $"{Reason(error, null, connectionString)}{(entra ? " " + EntraHint : "")}");
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
