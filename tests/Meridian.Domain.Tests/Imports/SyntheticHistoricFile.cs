using System.Globalization;
using System.Text;

namespace Meridian.Domain.Tests.Imports;

/// <summary>The outcome counts an import of the file must give into an empty system.</summary>
public sealed record ExpectedOutcomes(int New, int Duplicate, int Invalid, int Contradiction);

/// <summary>
/// A deterministic stand-in for <c>assembly_historic.csv</c>, shaped by the audit in map
/// ticket 17: 6,480 votes of 147 to 151 rows, 150 member codes, fully interleaved by vote.
/// The Performance test imports it; it is generated on demand from a seed and never committed.
/// </summary>
public sealed class SyntheticHistoricFile
{
    public const string Header = "assembly_vote_id,session,vote_date,resolution_title,country_code,country_name,ballot,published_at";

    private const int Votes = 6_480;
    private const int VotesPerYear = 720;
    private const int FirstYear = 2015;
    private const int Members = 150;

    public required byte[] Csv { get; init; }

    /// <summary>The matching <c>member_states.csv</c>.</summary>
    public required string MemberStatesCsv { get; init; }

    public required ExpectedOutcomes Expected { get; init; }

    public static SyntheticHistoricFile Generate(int seed = 20240229)
    {
        var random = new Random(seed);
        var codes = Enumerable.Range(0, Members).Select(Code).ToList();

        // The last state joins on the first day of the second year, so the 2015 votes have no
        // row for it, except two that carry one from before it was a member.
        var latecomer = codes[^1];
        var joined = new DateOnly(FirstYear + 1, 1, 1);
        var beforeJoining = Enumerable.Range(0, VotesPerYear).OrderBy(_ => random.Next()).Take(2).ToList();

        // Each defect kind lands on votes of its own, so a vote shows one thing at a time.
        var others = Enumerable.Range(0, Votes).Except(beforeJoining).OrderBy(_ => random.Next()).ToList();
        var next = 0;
        HashSet<int> Pick(int count) { var set = others.Skip(next).Take(count).ToHashSet(); next += count; return set; }
        var duplicated = Pick(30);
        var truncated = Pick(11);
        var shifted = Pick(4);
        var placeholders = Pick(10);
        var badBallots = Pick(5);
        string[] bad = ["0", "?", "YES", "0", "?"];
        // Two incomplete votes with no defects: the first misses 2 members, the second 3.
        var thin = others.Skip(next).Where(v => v >= VotesPerYear).Take(2).ToList();

        var lines = new List<string>(Votes * Members);
        var expected = new ExpectedOutcomes(0, 30, 32, 0);
        var badBallot = 0;
        for (var vote = 0; vote < Votes; vote++)
        {
            var year = FirstYear + vote / VotesPerYear;
            var number = vote % VotesPerYear + 1;
            // 60 vote days a year, 12 votes each.
            var date = new DateOnly(year, 1, 1).AddDays((number - 1) / 12 * 6);
            var id = $"MA/{year}/{number:000}";
            var title = $"Resolution {id} on item {number}";
            var session = year - 1945;
            var target = codes[random.Next(Members - 1)];
            foreach (var code in codes)
            {
                if (code == latecomer && date < joined && !beforeJoining.Contains(vote)) continue;
                if (thin.IndexOf(vote) is >= 0 and var missing && codes.IndexOf(code) < missing + 2) continue;
                var ballot = "YNAX"[random.Next(4)].ToString();
                var shownCode = code;
                if (code == target && placeholders.Contains(vote)) shownCode = "XXX";
                if (code == target && badBallots.Contains(vote)) ballot = bad[badBallot++];
                var line = Line(id, session, date, title, shownCode, shownCode == "XXX" ? "Unknown" : $"State {code}", ballot);
                lines.Add(line);
                if (code != target) continue;
                if (duplicated.Contains(vote)) lines.Add(line);
                if (truncated.Contains(vote)) lines.Add(line[..line.LastIndexOf(',')]);
                if (shifted.Contains(vote))
                    lines.Add($"{id},{session},{date:yyyy-MM-dd},{title}, shifted,{code},{ballot},{date.AddDays(2):yyyy-MM-dd}T00:00:00Z");
            }
        }

        // Fisher-Yates, so every vote's rows are scattered through the file.
        for (var i = lines.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (lines[i], lines[j]) = (lines[j], lines[i]);
        }

        var csv = new StringBuilder(Header).Append('\n');
        foreach (var line in lines) csv.Append(line).Append('\n');
        var members = new StringBuilder("country_code,country_name,member_from,member_to\n");
        foreach (var code in codes) members.Append($"{code},State {code},{(code == latecomer ? joined : new DateOnly(1990, 1, 1)):yyyy-MM-dd},\n");

        return new SyntheticHistoricFile
        {
            Csv = new UTF8Encoding(false).GetBytes(csv.ToString()),
            MemberStatesCsv = members.ToString(),
            Expected = expected with { New = lines.Count - expected.Duplicate - expected.Invalid },
        };
    }

    private static string Line(string id, int session, DateOnly date, string title, string code, string name, string ballot) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{id},{session},{date:yyyy-MM-dd},{title},{code},{name},{ballot},{date.AddDays(2):yyyy-MM-dd}T00:00:00Z");

    /// <summary>AAA, AAB, … AFX: three letters, none of them the placeholder XXX.</summary>
    private static string Code(int index) => $"A{(char)('A' + index / 26)}{(char)('A' + index % 26)}";
}
