// dotnet run --project src/Meridian.AzureCheck -- [--env .env] [--sheet <path to a real sheet PDF>]
// Run on your own machine; scripts/check-azure.sh asks for what is missing. Nothing is written or sent anywhere
// but to the services being checked.
namespace Meridian.AzureCheck;

/// <summary>A named entry point: top-level statements would add a second global <c>Program</c> beside the API's.</summary>
public static class CheckApp
{
    public static async Task<int> Main(string[] args)
    {
        string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

        var envPath = Option("--env") ?? ".env";
        var env = File.Exists(envPath) ? DotEnv.Parse(File.ReadAllLines(envPath)) : [];
        string? Setting(string key) => env.GetValueOrDefault(key) is { Length: > 0 } fromFile ? fromFile : Environment.GetEnvironmentVariable(key);

        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = cancel.Token;

        Console.WriteLine(File.Exists(envPath) ? $"Using {envPath}." : $"No {envPath} found; using the environment.");
        var results = new List<CheckResult>
        {
            await LiveChecks.BlobAsync(Setting("BLOB_CONNECTION_STRING"), Setting("BLOB_CONTAINER") ?? "imports", token),
            await LiveChecks.DatabaseAsync(Setting("AZURE_POSTGRES_CONNECTION_STRING"), token),
            await LiveChecks.DocumentIntelligenceAsync(
                LiveChecks.CreateReader(Setting("DOCINTEL_ENDPOINT"), Setting("DOCINTEL_KEY")), Option("--sheet"), token),
            await LiveChecks.ApplicationInsightsAsync(Setting("APPLICATIONINSIGHTS_CONNECTION_STRING"), token),
        };
        Console.WriteLine();
        Console.Write(Report.Render(results));
        return Report.ExitCode(results);
    }
}
