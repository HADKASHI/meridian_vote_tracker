using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meridian.Platform.Hosting;

/// <summary>
/// Off unless <c>Startup:CheckConnections</c> is true (<c>compose.azure.yaml</c> turns it on): then each failed
/// connection is logged by name when the process starts. With <c>Startup:CheckOnly</c> the process stops
/// afterwards, with exit code 1 if anything failed, so the migrate container can check before it migrates.
/// <c>Database:RequireTls</c> makes a connection string without TLS a failure.
/// </summary>
public static class StartupConnectionCheck
{
    public static IServiceCollection AddMeridianStartupConnectionCheck(this IServiceCollection services) =>
        services.AddHostedService<Check>();

    private sealed class Check(IConfiguration configuration, IHostApplicationLifetime lifetime, ILogger<Check> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!configuration.GetValue<bool>("Startup:CheckConnections")) return;

            var failures = await ConnectionCheck.RunAsync(
                configuration.GetConnectionString("Meridian"), configuration["Blob:ConnectionString"],
                configuration.GetValue<bool>("Database:RequireTls"), stoppingToken,
                blobServiceUri: configuration["Blob:ServiceUri"], checkBlob: true);
            foreach (var failure in failures)
                logger.LogError("Startup check failed for {Service}: {Message}", failure.Service, failure.Message);
            if (failures.Count == 0) logger.LogInformation("Startup check passed: the database and Blob storage answer.");

            if (!configuration.GetValue<bool>("Startup:CheckOnly")) return;
            // Stopping before the host has finished starting aborts the process instead of ending it.
            var started = new TaskCompletionSource();
            using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            using (stoppingToken.Register(() => started.TrySetCanceled(stoppingToken)))
                await started.Task;
            Environment.ExitCode = failures.Count == 0 ? 0 : 1;
            lifetime.StopApplication();
        }
    }
}
