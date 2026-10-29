namespace Meridian.Domain.Imports;

/// <summary>
/// Classifies one vote's rows against what is approved for it (map ticket 08 rules 3 to 7).
/// Pure: the caller reads the approved values under the vote's lock and writes the verdict.
/// </summary>
public static class Classifier
{
    public static VoteVerdict Classify(IReadOnlyList<ExportRow> rows, ApprovedVote approved, MemberStates members)
    {
        var normalised = rows.Select(r => (Row: r, N: CheckMembership(RowNormaliser.Normalise(r, members), approved, members))).ToList();
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
        var (detailsLink, detailsPart) = valid.Count == 0 ? (Link.None, null) : CompareDetails(valid[0].N.Details!, approved);
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
            if (detailsLink.Kind == LinkKind.NewRevision) newDetails ??= n.Details;
            verdicts.Add(AgainstApproved(row, n, approved, detailsLink, detailsPart));
        }
        // Only a row that reaches the comparison carries the details; contradictions open nothing.
        var newDetailsConflict = verdicts.Any(v => v.Details.Kind == LinkKind.NewConflict) ? valid[0].N.Details : null;
        return new VoteVerdict(verdicts, newDetails, newDetailsConflict);
    }

    /// <summary>
    /// Rule 11: the members on the vote's approved date with no approved country vote once
    /// this verdict is written, by code. A vote with no approved details has no date yet.
    /// </summary>
    public static IReadOnlyList<string> MissingMembers(VoteVerdict verdict, ApprovedVote approved, MemberStates members)
    {
        if ((approved.Details?.Value ?? verdict.NewDetails) is not { } details) return [];
        var voted = approved.Ballots.Keys
            .Concat(verdict.Rows.Where(r => r.Ballot.Kind == LinkKind.NewRevision).Select(r => r.Values!.StateCode!))
            .ToHashSet();
        return members.MembersOn(details.VoteDate).Where(code => !voted.Contains(code)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Rule 12, and rule 5's twist: membership is checked on the approved date when the vote
    /// has one, so a row carrying new details is judged against the details that count.
    /// </summary>
    private static NormalisedRow CheckMembership(NormalisedRow n, ApprovedVote approved, MemberStates members)
    {
        if (n.Invalid is not null) return n;
        var date = approved.Details?.Value.VoteDate ?? n.Details!.VoteDate;
        var code = n.Values.StateCode!;
        return members.IsMemberOn(code, date)
            ? n
            : n with { Invalid = ReasonCode.NotMemberOnDate, Message = $"{code} was not a member on {date:yyyy-MM-dd}.", Details = null, Ballot = null };
    }

    /// <summary>The vote's details against the approved ones (rules 5 to 7): one verdict for the whole vote.</summary>
    private static (Link Link, Part? Part) CompareDetails(VoteDetails incoming, ApprovedVote approved)
    {
        if (approved.Details is not { } current) return (Link.NewRevision, null);
        if (DetailsKey(incoming) == DetailsKey(current.Value)) return (Link.ToApproved(current.RevisionId), null);

        var known = approved.Conflicts.FirstOrDefault(c => c.Subject == ConflictSubject.Details
            && c.ComparedRevisionId == current.RevisionId && DetailsKey(c.IncomingDetails!) == DetailsKey(incoming));
        return AgainstConflict(known, "These vote details", plural: true,
            "The vote details differ from the approved ones; one conflict is opened for the vote and the approved details stay.");
    }

    /// <summary>A valid row against approved data: its ballot compared on its own, whatever its details did.</summary>
    private static RowVerdict AgainstApproved(ExportRow row, NormalisedRow n, ApprovedVote approved, Link details, Part? detailsPart)
    {
        var state = n.Values.StateCode!;
        var ballot = n.Ballot!.Value;
        Link ballotLink;
        Part ballotPart;
        if (!approved.Ballots.TryGetValue(state, out var current))
            (ballotLink, ballotPart) = (Link.NewRevision, new Part(ReasonCode.NewValue, detailsPart is null
                ? "New value: nothing was approved for it yet."
                : "The ballot is new and is stored under the approved details."));
        else if (current.Value == ballot)
            (ballotLink, ballotPart) = (Link.ToApproved(current.RevisionId), new Part(ReasonCode.MatchesApproved, detailsPart is null
                ? "Matches the approved data."
                : "The ballot matches the approved one."));
        else
        {
            var known = approved.Conflicts.FirstOrDefault(c => c.Subject == ConflictSubject.Ballot
                && c.ComparedRevisionId == current.RevisionId && c.IncomingBallot == ballot);
            (ballotLink, ballotPart) = AgainstConflict(known, $"Ballot {ballot}", plural: false,
                $"Ballot {ballot} differs from the approved {current.Value}; a conflict is opened and {current.Value} stays approved.");
        }

        var conflictParts = new[] { detailsPart, ballotLink.IsConflict ? ballotPart : null }.OfType<Part>().ToList();
        var outcome = conflictParts.Count > 0 ? Outcome.Conflict
            : ballotLink.Kind == LinkKind.NewRevision ? Outcome.New
            : Outcome.Unchanged;
        var reason = conflictParts.Count == 0 ? ballotPart.Reason
            : conflictParts.Any(p => p.Reason == ReasonCode.DiffersFromApproved) ? ReasonCode.DiffersFromApproved
            : conflictParts.Any(p => p.Reason == ReasonCode.PendingConflict) ? ReasonCode.PendingConflict
            : ReasonCode.PreviouslyRejected;
        var message = string.Join(" ", new[] { detailsPart?.Message, ballotPart.Message }.OfType<string>());
        return Verdict(row, n, outcome, reason, message, ballotLink, details);
    }

    /// <summary>
    /// Rules 6 and 7 for either subject: no matching conflict opens one; a matching conflict
    /// is linked to, as pending or, once rejected, as never reopening.
    /// </summary>
    private static (Link, Part) AgainstConflict(KnownConflict? known, string value, bool plural, string opens) => known switch
    {
        null => (Link.NewConflict, new Part(ReasonCode.DiffersFromApproved, opens)),
        { Decision: Decision.Reject } => (Link.ToConflict(known.Id), new Part(ReasonCode.PreviouslyRejected,
            $"{value} {(plural ? "were" : "was")} already rejected in conflict #{known.Id}; it does not reopen.")),
        _ => (Link.ToConflict(known.Id), new Part(ReasonCode.PendingConflict,
            $"{value} {(plural ? "are" : "is")} already pending as conflict #{known.Id}.")),
    };

    private sealed record Part(ReasonCode Reason, string Message);

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
