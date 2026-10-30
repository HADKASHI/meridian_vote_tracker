// dotnet run --project src/Meridian.AzureCheck -- [--env .env] [--sheet <path to a real sheet PDF>] [--device-code]
// --device-code signs in to Microsoft Entra ID with a code you approve in a browser, for a machine without `az login`.
// Run on your own machine; scripts/check-azure.sh asks for what is missing. Nothing is written or sent anywhere
// but to the services being checked.
using Azure.Core;
using Azure.Identity;

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

        TokenCredential? credential = args.Contains("--device-code")
            ? new DeviceCodeCredential(new DeviceCodeCredentialOptions { TenantId = Setting("AZURE_TENANT_ID") })
            : null;

        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(credential is null ? 3 : 6));
        var token = cancel.Token;

        // The sign-in waits for a person, which the services' own short timeouts must not cut off: do it first, once.
        // The other services then take their tokens from the same sign-in without asking again.
        if (credential is not null)
            foreach (var scope in new[] { "https://storage.azure.com/.default", Meridian.Data.DatabaseAccess.TokenScope, "https://cognitiveservices.azure.com/.default" })
                try { await credential.GetTokenAsync(new TokenRequestContext([scope]), token); }
                catch (Exception error) when (!token.IsCancellationRequested) { Console.WriteLine($"No token for {scope}: {error.GetType().Name}"); }

        Console.WriteLine(File.Exists(envPath) ? $"Using {envPath}." : $"No {envPath} found; using the environment.");
        var results = new List<CheckResult>
        {
            await LiveChecks.BlobAsync(Setting("BLOB_CONNECTION_STRING"), Setting("BLOB_SERVICE_URI"), Setting("BLOB_CONTAINER") ?? "imports", token, credential),
            await LiveChecks.DatabaseAsync(Setting("AZURE_POSTGRES_CONNECTION_STRING"), token, credential: credential),
            await LiveChecks.DocumentIntelligenceAsync(
                LiveChecks.CreateReader(Setting("DOCINTEL_ENDPOINT"), Setting("DOCINTEL_KEY"), credential), Option("--sheet"), token),
            await LiveChecks.ApplicationInsightsAsync(Setting("APPLICATIONINSIGHTS_CONNECTION_STRING"), token),
        };
        Console.WriteLine();
        Console.Write(Report.Render(results));
        return Report.ExitCode(results);
    }
}
