namespace Meridian.Domain.Imports;

/// <summary>Why a row got its outcome. Stored as its snake_case name; report information, so no database CHECK.</summary>
public enum ReasonCode
{
    NewValue,
    FieldCount,
    MissingField,
    UnknownCountryCode,
    InvalidBallot,
    EmptyBallot,
    InvalidDate,
    InvalidSession,
    NotMemberOnDate,
}

/// <summary>A vote's session, date and title.</summary>
public sealed record VoteDetails(int Session, DateOnly VoteDate, string Title);

/// <summary>One membership period: both ends inclusive; no end means still a member.</summary>
public sealed record MembershipSpan(DateOnly From, DateOnly? To)
{
    public bool Covers(DateOnly date) => From <= date && (To is null || date <= To);
}

/// <summary>Every member state the Assembly has had, by code, with its periods.</summary>
public sealed record MemberStates(IReadOnlyDictionary<string, IReadOnlyList<MembershipSpan>> Periods)
{
    public bool IsKnown(string code) => Periods.ContainsKey(code);

    public bool IsMemberOn(string code, DateOnly date) =>
        Periods.TryGetValue(code, out var spans) && spans.Any(s => s.Covers(date));
}

/// <summary>An approved value with the revision it lives in.</summary>
public sealed record Approved<T>(long RevisionId, T Value);

/// <summary>What is approved for one vote when its rows are classified, read under the vote's lock.</summary>
public sealed record ApprovedVote(Approved<VoteDetails>? Details, IReadOnlyDictionary<string, Approved<Ballot>> Ballots)
{
    public static readonly ApprovedVote Nothing = new(null, new Dictionary<string, Approved<Ballot>>());
}

/// <summary>A row's values as stored on its row record: normalised where valid, as found otherwise.</summary>
public sealed record RowValues(
    string? AssemblyVoteId, string? StateCode, string? CountryName, string? BallotText, Ballot? Ballot,
    int? Session, DateOnly? VoteDate, string? Title, DateTimeOffset? PublishedAt);

public enum LinkKind { None, NewRevision, Approved }

/// <summary>
/// What a row points at for one subject (its ballot or its vote's details): nothing, the
/// revision it brings in as new, or the approved revision it matched.
/// </summary>
public readonly record struct Link(LinkKind Kind, long? RevisionId = null)
{
    public static readonly Link None = new(LinkKind.None);
    public static readonly Link NewRevision = new(LinkKind.NewRevision);
    public static Link ToApproved(long revisionId) => new(LinkKind.Approved, revisionId);
}

/// <summary>The verdict on one line, fixed at the moment of import.</summary>
public sealed record RowVerdict(
    int LineNo, string RawText, Outcome Outcome, ReasonCode Reason, string Message,
    IReadOnlyList<string> Normalisations, RowValues? Values, Link Ballot, Link Details);

/// <summary>
/// One vote's verdicts and what they change: the details to approve when the vote has
/// none (the first row linking <see cref="Link.NewRevision"/> is their origin), and the
/// ballots each row with a new ballot brings in.
/// </summary>
public sealed record VoteVerdict(IReadOnlyList<RowVerdict> Rows, VoteDetails? NewDetails);
