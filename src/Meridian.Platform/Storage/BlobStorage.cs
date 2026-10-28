using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meridian.Platform.Storage;

/// <summary>Uploaded files live in Azure Blob (Azurite locally); switching is a connection string.</summary>
public static class BlobStorage
{
    public static IServiceCollection AddMeridianBlobStorage(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return new BlobContainerClient(
                configuration["Blob:ConnectionString"] ?? throw new InvalidOperationException("Blob:ConnectionString is not set."),
                configuration["Blob:Container"] ?? "imports");
        });
        services.AddHostedService<BlobContainerInitializer>();
        return services;
    }

    /// <summary>Creates the container on startup if it is missing.</summary>
    private sealed class BlobContainerInitializer(IServiceProvider services, ILogger<BlobContainerInitializer> logger)
        : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await services.GetRequiredService<BlobContainerClient>().CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Readiness reports it; the process stays up so it recovers when storage does.
                logger.LogError(error, "Could not create the blob container");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
