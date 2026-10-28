using Meridian.Domain.Imports;

namespace Meridian.Domain.Tests.Imports;

/// <summary>Map ticket 08 rules 3 to 7: one outcome per row, first match wins.</summary>
public sealed class ClassifierTests
{
    private static readonly MemberStates Members = new(new Dictionary<string, IReadOnlyList<MembershipSpan>>
    {
        ["BRA"] = [new(new DateOnly(1990, 1, 1), null)],
        ["FRA"] = [new(new DateOnly(1990, 1, 1), null)],
        ["IND"] = [new(new DateOnly(1990, 1, 1), null)],
        ["JPN"] = [new(new DateOnly(1990, 1, 1), null)],
        ["NZL"] = [new(new DateOnly(1990, 1, 1), new DateOnly(2021, 6, 30))],
    });

    private static int _line = 1;

    private static ExportRow Row(string code, string ballot, string vote = "MA/2024/101", string session = "79",
        string date = "2024-01-10", string title = "Oceans and the Law of the Sea", string? name = null)
    {
        var line = ++_line;
        return new ExportRow(line, $"{vote},{session},{date},{title},{code},{ballot}", 6,
            new ExportFields(vote, session, date, title, code, ballot, name, null));
    }

    private static VoteVerdict Classify(IReadOnlyList<ExportRow> rows, ApprovedVote? approved = null) =>
        Classifier.Classify(rows, approved ?? ApprovedVote.Nothing, Members);

    [Fact]
    public void Rule3_with_nothing_approved_every_valid_row_is_new()
    {
        var verdict = Classify([Row("BRA", "Y"), Row("IND", "N")]);

        Assert.Equal([Outcome.New, Outcome.New], verdict.Rows.Select(r => r.Outcome));
        Assert.All(verdict.Rows, r => Assert.Equal(ReasonCode.NewValue, r.Reason));
        Assert.All(verdict.Rows, r => Assert.Equal(LinkKind.NewRevision, r.Ballot.Kind));
        Assert.All(verdict.Rows, r => Assert.Equal(LinkKind.NewRevision, r.Details.Kind));
        Assert.Equal(new VoteDetails(79, new DateOnly(2024, 1, 10), "Oceans and the Law of the Sea"), verdict.NewDetails);
        Assert.Equal(["BRA", "IND"], verdict.Rows.Select(r => r.Values!.StateCode));
        Assert.Equal([Ballot.Y, Ballot.N], verdict.Rows.Select(r => r.Values!.Ballot));
    }
}
