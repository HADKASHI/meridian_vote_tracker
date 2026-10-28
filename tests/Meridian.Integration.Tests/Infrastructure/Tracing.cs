using System.Diagnostics;

namespace Meridian.Integration.Tests.Infrastructure;

public static class Tracing
{
    /// <summary>
    /// Makes ASP.NET Core start a request activity, as the console logger does in the real
    /// processes; the test host clears logging, so without a listener there is no trace.
    /// </summary>
    public static IDisposable ListenToRequests()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
