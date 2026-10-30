using Azure.Core;
using Azure.Storage.Blobs;
using Meridian.Data;

namespace Meridian.Platform.Storage;

/// <summary>
/// How Blob storage is reached (ADR 0009): by <c>Blob:ServiceUri</c> with a Microsoft Entra ID sign-in, or by
/// <c>Blob:ConnectionString</c> (Azurite locally, or a storage account's key). A service address, when set, wins, so a
/// compose file that always carries Azurite's connection string can still be pointed at a real account.
/// </summary>
public static class BlobAccess
{
    public static BlobServiceClient Service(
        string? connectionString, string? serviceUri, TokenCredential? credential = null, BlobClientOptions? options = null)
    {
        if (!string.IsNullOrWhiteSpace(serviceUri))
            return new BlobServiceClient(new Uri(serviceUri), credential ?? AzureCredentials.Default, options);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return new BlobServiceClient(connectionString, options);
        throw new InvalidOperationException("Neither Blob:ServiceUri nor Blob:ConnectionString is set.");
    }
}
