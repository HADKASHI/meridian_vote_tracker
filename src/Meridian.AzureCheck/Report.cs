using System.Text;

namespace Meridian.AzureCheck;

public static class Report
{
    public static string Render(IReadOnlyList<CheckResult> results)
    {
        var text = new StringBuilder();
        foreach (var result in results)
            text.AppendLine($"{Tag(result.Status)}  {result.Name}: {result.Detail}");
        var passed = results.Count(r => r.Status == CheckStatus.Passed);
        text.AppendLine().AppendLine($"{passed} of {results.Count} passed.");
        return text.ToString();
    }

    /// <summary>0 only when every check passed; a check that was skipped has not proven anything.</summary>
    public static int ExitCode(IReadOnlyList<CheckResult> results) => results.All(r => r.Status == CheckStatus.Passed) ? 0 : 1;

    private static string Tag(CheckStatus status) => status switch
    {
        CheckStatus.Passed => "PASS",
        CheckStatus.Failed => "FAIL",
        _ => "SKIP",
    };
}

/// <summary>Reads a <c>.env</c> the way compose does (the rest of the line is the value), into a dictionary and nowhere else.</summary>
public static class DotEnv
{
    public static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>();
        foreach (var line in lines.Select(l => l.Trim()))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var at = line.IndexOf('=');
            if (at > 0) values[line[..at].Trim()] = line[(at + 1)..].Trim();
        }
        return values;
    }
}
