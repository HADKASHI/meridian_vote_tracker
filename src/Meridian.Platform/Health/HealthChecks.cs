using Azure.Storage.Blobs;
using Meridian.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Meridian.Platform.Health;

/// <summary><c>/health/live</c> is the process only; <c>/health/ready</c> also needs PostgreSQL and Blob.</summary>
public static class HealthChecks
{
    private const string Ready = "ready";

    public static IServiceCollection AddMeridianHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseCheck>("database", tags: [Ready])
            .AddCheck<BlobCheck>("blob", tags: [Ready]);
        return services;
    }

    public static IEndpointRouteBuilder MapMeridianHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(Ready) })
            .AllowAnonymous();
        return app;
    }

    private sealed class DatabaseCheck(MeridianDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }

    private sealed class BlobCheck(BlobContainerClient container) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
        {
            try
            {
                return (await container.ExistsAsync(cancellationToken)).Value
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The blob container does not exist.");
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy("Blob storage is not reachable.");
            }
        }
    }
}
