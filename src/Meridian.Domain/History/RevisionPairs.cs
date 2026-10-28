namespace Meridian.Domain.History;

/// <summary>
/// Builds from → to pairs over an insert-only history (map ticket 04 rule 7, ADR 0001): a
/// change stores only its new value, and "previous" is the earlier revision of the same
/// subject in history-id order, never by timestamp.
/// </summary>
public static class RevisionPairs
{
    /// <param name="revisions">Revision ids with their subject: a vote's details, a vote's position, or one member state's country vote in a vote.</param>
    /// <returns>For each revision, the id of the one before it of the same subject, or null for the first.</returns>
    public static IReadOnlyDictionary<long, long?> PreviousOf<TSubject>(IEnumerable<(long Id, TSubject Subject)> revisions)
        where TSubject : notnull
    {
        var previous = new Dictionary<long, long?>();
        foreach (var subject in revisions.GroupBy(r => r.Subject))
        {
            long? before = null;
            foreach (var id in subject.Select(r => r.Id).Order())
            {
                previous[id] = before;
                before = id;
            }
        }
        return previous;
    }
}
