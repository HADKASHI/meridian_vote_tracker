using System.Globalization;
using System.Text;
using Meridian.Domain.Csv;
using Meridian.Domain.Imports;

namespace Meridian.Domain.Tests.Imports;

/// <summary>
/// The synthetic stand-in for the historic file (map ticket 17), used by the Performance
/// test. It is generated, never committed, so these tests pin its shape.
/// </summary>
public sealed class SyntheticHistoricFileTests
{
    private static readonly SyntheticHistoricFile File = SyntheticHistoricFile.Generate();

    [Fact]
    public void The_same_seed_gives_the_same_bytes_and_another_seed_does_not()
    {
        Assert.Equal(File.Csv, SyntheticHistoricFile.Generate().Csv);
        Assert.NotEqual(File.Csv, SyntheticHistoricFile.Generate(seed: 2).Csv);
    }

    [Fact]
    public void It_is_shaped_like_the_historic_file()
    {
        var perVote = new Dictionary<string, int>();
        var codes = new HashSet<string>();
        string? previous = null;
        var (total, neighbours) = (0, 0);
        foreach (var row in Rows(File.Csv))
        {
            perVote[row[0]] = perVote.GetValueOrDefault(row[0]) + 1;
            codes.Add(row[4]);
            if (row[0] == previous) neighbours++;
            (previous, total) = (row[0], total + 1);
        }

        Assert.Equal(6_480, perVote.Count);
        Assert.All(perVote.Values, n => Assert.InRange(n, 147, 151));
        Assert.Equal((147, 151), (perVote.Values.Min(), perVote.Values.Max()));
        Assert.Equal(150, codes.Count(c => c.Length == 3 && c != "XXX"));
        // Fully interleaved: no vote's rows sit side by side for long.
        Assert.True(neighbours < total / 100, $"{neighbours} neighbouring rows share a vote");
        Assert.InRange(File.Csv.Length, 80_000_000, 120_000_000);
    }

    [Fact]
    public async Task Classifying_it_gives_the_audited_counts_with_every_defect_kind_present()
    {
        var parsed = await ExportParser.ParseAsync(new MemoryStream(File.Csv), cancellationToken: TestContext.Current.CancellationToken);
        var members = Members(File.MemberStatesCsv);

        var verdicts = parsed.Rows.GroupBy(RowNormaliser.VoteKey)
            .SelectMany(g => Classifier.Classify(g.ToList(), ApprovedVote.Nothing, members).Rows)
            .ToList();

        Assert.Equal(parsed.DataRows, verdicts.Count);
        // The audit's 30 duplicates and 32 invalid rows (11 truncated, 4 shifted, 10 placeholders, 5 bad ballots, 2 before joining), and no contradictions.
        var counted = new ExpectedOutcomes(
            verdicts.Count(v => v.Outcome == Outcome.New), verdicts.Count(v => v.Outcome == Outcome.Duplicate),
            verdicts.Count(v => v.Outcome == Outcome.Invalid), verdicts.Count(v => v.Outcome == Outcome.Contradiction));
        Assert.Equal(new ExpectedOutcomes(parsed.DataRows - 62, 30, 32, 0), counted);
        Assert.Equal(counted, File.Expected);
        Assert.Equal(
            new Dictionary<ReasonCode, int>
            {
                [ReasonCode.FieldCount] = 11, [ReasonCode.UnknownCountryCode] = 10 + 4, [ReasonCode.InvalidBallot] = 5,
                [ReasonCode.NotMemberOnDate] = 2,
            }.OrderBy(p => p.Key),
            verdicts.Where(v => v.Outcome == Outcome.Invalid).GroupBy(v => v.Reason)
                .ToDictionary(g => g.Key, g => g.Count()).OrderBy(p => p.Key));
    }

    private static MemberStates Members(string csv)
    {
        using var reader = new StringReader(csv);
        var periods = CsvReader.Read(reader).Skip(1).Where(r => r.Fields.Count == 4).ToDictionary(
            r => r.Fields[0],
            r => (IReadOnlyList<MembershipSpan>)[new MembershipSpan(DateOnly.Parse(r.Fields[2], CultureInfo.InvariantCulture),
                r.Fields[3] == "" ? null : DateOnly.Parse(r.Fields[3], CultureInfo.InvariantCulture))]);
        return new MemberStates(periods);
    }

    /// <summary>The data rows, split into fields, without the header.</summary>
    private static IEnumerable<IReadOnlyList<string>> Rows(byte[] csv)
    {
        using var reader = new StreamReader(new MemoryStream(csv), new UTF8Encoding(false, true));
        foreach (var record in CsvReader.Read(reader).Skip(1))
            if (record.Fields.Count >= 6) yield return record.Fields;
    }
}
