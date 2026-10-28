namespace Meridian.Integration.Tests.Infrastructure;

/// <summary>
/// For race tests: starts every action, holds them until all are ready, then releases
/// them together, so two real connections hit the database at the same moment.
/// </summary>
public static class RaceBarrier
{
    public static async Task<T[]> RunTogetherAsync<T>(int count, Func<int, Task<T>> action)
    {
        var ready = new CountdownEvent(count);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
        {
            ready.Signal();
            await go.Task;
            return await action(i);
        })).ToArray();

        ready.Wait(TimeSpan.FromSeconds(10));
        go.SetResult();
        return await Task.WhenAll(tasks);
    }
}
