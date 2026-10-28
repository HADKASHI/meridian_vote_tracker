using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Fixtures;

public sealed class RaceBarrierTests
{
    [Fact]
    public async Task Two_actions_are_released_together()
    {
        var arrived = 0;
        var bothArrived = new TaskCompletionSource();

        // Each action waits until the other has also started: only a joint release passes.
        var results = await RaceBarrier.RunTogetherAsync(2, async i =>
        {
            if (Interlocked.Increment(ref arrived) == 2) bothArrived.SetResult();
            await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return i;
        });

        Assert.Equal([0, 1], results);
    }
}
