using Meridian.Data.Entities;
using Meridian.Domain;

namespace Meridian.Conflicts;

/// <summary>Builds the value on either side of a conflict.</summary>
internal static class ConflictValues
{
    public static ConflictValueDto Ballot(Ballot ballot) => new(ballot, null, null, null);

    public static ConflictValueDto Details(int session, DateOnly voteDate, string title) => new(null, session, voteDate, title);

    public static ConflictValueDto Incoming(Conflict c) => c.Subject == ConflictSubject.Ballot
        ? Ballot(c.IncomingBallot!.Value)
        : Details(c.IncomingSession!.Value, c.IncomingVoteDate!.Value, c.IncomingTitle!);
}
