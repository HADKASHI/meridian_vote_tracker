namespace Meridian.Domain.Imports;

/// <summary>
/// Classifies one vote's rows against what is approved for it (map ticket 08 rules 3 to 7).
/// Pure: the caller reads the approved values under the vote's lock and writes the verdict.
/// </summary>
public static class Classifier
{
    public static VoteVerdict Classify(IReadOnlyList<ExportRow> rows, ApprovedVote approved, MemberStates members)
    {
        var normalised = rows.Select(r => (Row: r, N: RowNormaliser.Normalise(r, members))).ToList();
        var valid = normalised.Where(x => x.N.Invalid is null).ToList();

        // Rule 4: a file that disagrees with itself about the details holds back the whole vote.
        var detailKeys = valid.Select(x => DetailsKey(x.N.Details!)).Distinct().Count();
        if (detailKeys > 1) return HeldBack(normalised, valid);

        // Rule 3: one state with different ballots in one vote; none of them is stored.
        var contradicted = valid.GroupBy(x => x.N.Values.StateCode!)
            .Where(g => g.Select(x => x.N.Ballot).Distinct().Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Row.LineNo).ToList());

        var verdicts = new List<RowVerdict>(rows.Count);
        var firstLine = new Dictionary<string, int>();
        VoteDetails? newDetails = null;
        foreach (var (row, n) in normalised)
        {
            if (n.Invalid is { } reason)
            {
                verdicts.Add(Verdict(row, n, Outcome.Invalid, reason, n.Message!));
                continue;
            }
            var state = n.Values.StateCode!;
            if (contradicted.TryGetValue(state, out var lines))
            {
                verdicts.Add(Verdict(row, n, Outcome.Contradiction, ReasonCode.BallotContradiction,
                    $"{state} has different ballots in this vote on lines {string.Join(", ", lines)}; none is stored."));
                continue;
            }
            if (!firstLine.TryAdd(state, row.LineNo))
            {
                verdicts.Add(Verdict(row, n, Outcome.Duplicate, ReasonCode.Duplicate, $"Identical to line {firstLine[state]}."));
                continue;
            }
            newDetails ??= n.Details;
            verdicts.Add(Verdict(row, n, Outcome.New, ReasonCode.NewValue, "New value: nothing was approved for it yet.",
                Link.NewRevision, Link.NewRevision));
        }
        return new VoteVerdict(verdicts, newDetails);
    }

    private static VoteVerdict HeldBack(List<(ExportRow Row, NormalisedRow N)> normalised, List<(ExportRow Row, NormalisedRow N)> valid)
    {
        var first = valid[0];
        var disagree = new List<string>();
        if (valid.Select(x => x.N.Details!.Session).Distinct().Count() > 1) disagree.Add("session");
        if (valid.Select(x => x.N.Details!.VoteDate).Distinct().Count() > 1) disagree.Add("vote_date");
        if (valid.Select(x => RowNormaliser.ComparisonKey(x.N.Details!.Title)).Distinct().Count() > 1) disagree.Add("resolution_title");
        var reason = $"The rows of this vote disagree on {string.Join(", ", disagree)}, so the whole vote is held back and nothing from this file is stored for it.";

        return new VoteVerdict(normalised.Select(x => x.N.Invalid is { } invalid
            ? Verdict(x.Row, x.N, Outcome.Invalid, invalid, x.N.Message!)
            : Verdict(x.Row, x.N, Outcome.Contradiction, ReasonCode.DetailsContradiction,
                x.Row == first.Row ? reason : $"Held back with its vote: see line {first.Row.LineNo}.")).ToList(), null);
    }

    /// <summary>Details compare on session, date and the title's comparison key (rule 2).</summary>
    private static (int, DateOnly, string) DetailsKey(VoteDetails details) =>
        (details.Session, details.VoteDate, RowNormaliser.ComparisonKey(details.Title));

    private static RowVerdict Verdict(ExportRow row, NormalisedRow n, Outcome outcome, ReasonCode reason, string message,
        Link? ballot = null, Link? details = null) =>
        new(row.LineNo, row.RawText, outcome, reason, message, n.Normalisations, n.Values, ballot ?? Link.None, details ?? Link.None);
}
