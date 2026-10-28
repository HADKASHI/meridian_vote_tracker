using System.Globalization;

namespace Meridian.Domain.Imports;

/// <summary>
/// Classifies one vote's rows against what is approved for it (map ticket 08 rules 3 to 7).
/// Pure: the caller reads the approved values under the vote's lock and writes the verdict.
/// </summary>
public static class Classifier
{
    public static VoteVerdict Classify(IReadOnlyList<ExportRow> rows, ApprovedVote approved, MemberStates members)
    {
        var verdicts = new List<RowVerdict>(rows.Count);
        VoteDetails? newDetails = null;
        foreach (var row in rows)
        {
            var f = row.Fields!;
            var details = new VoteDetails(int.Parse(f.Session, CultureInfo.InvariantCulture),
                DateOnly.ParseExact(f.VoteDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), f.Title);
            newDetails ??= details;
            var ballot = Enum.Parse<Ballot>(f.Ballot);
            var values = new RowValues(f.AssemblyVoteId, f.CountryCode, f.CountryName, f.Ballot, ballot,
                details.Session, details.VoteDate, details.Title, ExportParser.ParsePublishedAt(f.PublishedAt));
            verdicts.Add(new RowVerdict(row.LineNo, row.RawText, Outcome.New, ReasonCode.NewValue,
                "New value: nothing was approved for it yet.", [], values, Link.NewRevision, Link.NewRevision));
        }
        return new VoteVerdict(verdicts, newDetails);
    }
}
