using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meridian.Platform.Hosting;

public static class Observability
{
    /// <summary>
    /// Built-in console logging with scopes, so every line carries the trace and span ids
    /// plus the user, job, import or vote in scope: readable under Development, JSON
    /// otherwise. Application Insights only when its connection string is configured.
    /// Log ids, counts and timings, never client data (rows, titles, ballots, comments).
    /// </summary>
    public static void AddMeridianObservability(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.Configure(o => o.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);
        if (builder.Environment.IsDevelopment())
            builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; });
        else
            builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            builder.Services.AddOpenTelemetry().UseAzureMonitor()
                .WithTracing(tracing => tracing.AddSource(Jobs.JobProcessor.ActivitySourceName));
    }
}
