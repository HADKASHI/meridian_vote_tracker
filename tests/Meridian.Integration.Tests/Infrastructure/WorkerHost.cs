using Meridian.Platform.Hosting;
using Meridian.Platform.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meridian.Integration.Tests.Infrastructure;

/// <summary>
/// The worker's services against a test database, with the given job handlers. Tests call
/// its <see cref="JobProcessor"/> on demand; only the BackgroundService test starts it.
/// </summary>
public static class WorkerHost
{
    public static IHost Build(string connectionString, IEnumerable<IJobHandler> handlers,
        IDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Meridian"] = connectionString,
            ["Jobs:PollInterval"] = "00:00:00.100",
            ["Jobs:HeartbeatInterval"] = "00:00:00.200",
            ["Jobs:StaleAfter"] = "00:01:00",
            ["Jobs:MaxAttempts"] = "3",
            ["Jobs:ImportTimeout"] = "00:00:30",
            ["Jobs:SnapshotTimeout"] = "00:00:30",
            ["Imports:MaxConcurrent"] = "1",
        });
        if (settings is not null) builder.Configuration.AddInMemoryCollection(settings);
        builder.AddMeridianWorkerPlatform();
        builder.Logging.ClearProviders();
        foreach (var handler in handlers) builder.Services.AddSingleton(handler);
        return builder.Build();
    }

    public static JobProcessor Processor(this IHost host) => host.Services.GetRequiredService<JobProcessor>();
}

/// <summary>A job handler whose work the test supplies.</summary>
public sealed class TestJobHandler(Domain.JobKind kind, Func<JobContext, CancellationToken, Task>? run = null) : IJobHandler
{
    public Domain.JobKind Kind => kind;

    public Task RunAsync(JobContext context, CancellationToken cancellationToken) =>
        run?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
}
