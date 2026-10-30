using Meridian.Platform.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Meridian.Platform.Hosting;

public static class WorkerHosting
{
    /// <summary>The worker's shared services: data, logs and the job runner. Slices add only their job handler.</summary>
    public static void AddMeridianWorkerPlatform(this IHostApplicationBuilder builder)
    {
        builder.AddMeridianObservability();
        builder.Services.AddMeridianDb();
        builder.Services.AddMeridianJobRunner(builder.Configuration);
        builder.Services.AddHostedService<JobWorker>();
    }

    public static IServiceCollection AddMeridianJobRunner(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection("Jobs"))
            .Configure(o =>
            {
                o.MaxConcurrentImports = configuration.GetValue("Imports:MaxConcurrent", o.MaxConcurrentImports);
                o.MaxConcurrentExtractions = configuration.GetValue("Extractions:MaxConcurrent", o.MaxConcurrentExtractions);
            });
        services.AddSingleton(provider => NpgsqlDataSource.Create(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("Meridian")
                ?? throw new InvalidOperationException("ConnectionStrings:Meridian is not set.")));
        services.AddSingleton<JobProcessor>();
        return services;
    }

    /// <summary>Registers a slice's handler for its job kind.</summary>
    public static IServiceCollection AddJobHandler<THandler>(this IServiceCollection services)
        where THandler : class, IJobHandler =>
        services.AddScoped<IJobHandler, THandler>();
}
