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
            var n = RowNormaliser.Normalise(row, members);
            if (n.Invalid is { } reason)
            {
                verdicts.Add(new RowVerdict(row.LineNo, row.RawText, Outcome.Invalid, reason, n.Message!,
                    n.Normalisations, n.Values, Link.None, Link.None));
                continue;
            }
            newDetails ??= n.Details;
            verdicts.Add(new RowVerdict(row.LineNo, row.RawText, Outcome.New, ReasonCode.NewValue,
                "New value: nothing was approved for it yet.", n.Normalisations, n.Values, Link.NewRevision, Link.NewRevision));
        }
        return new VoteVerdict(verdicts, newDetails);
    }
}
