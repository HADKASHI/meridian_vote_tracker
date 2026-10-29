using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.Platform.Jobs;

/// <summary>
/// The worker loop: every <c>Jobs:PollInterval</c> it sweeps stalled jobs, then claims and
/// starts every job allowed to start, so a snapshot runs alongside a long import.
/// </summary>
public sealed class JobWorker(JobProcessor processor, IOptions<JobOptions> options, ILogger<JobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var running = new List<Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.SweepAsync(stoppingToken);
                while (await processor.ClaimNextAsync(stoppingToken) is { } job)
                    running.Add(processor.RunClaimedAsync(job, stoppingToken));
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Could not reach the job queue; retrying");
            }
            catch (OperationCanceledException)
            {
                // Stopped mid-poll: still wait below for the running jobs to be handed back.
                break;
            }

            running.RemoveAll(t => t.IsCompleted);
            try { await Task.Delay(options.Value.PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        // Each job finishes its current step, then is handed back to the queue.
        await Task.WhenAll(running);
    }
}
