using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meridian.Snapshots;

/// <summary>The JSON a snapshot keeps: its selection as entered, and each warning's detail as it stood.</summary>
internal static class SnapshotJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string WriteSelection(SnapshotSelectionDto selection) => JsonSerializer.Serialize(selection, Options);

    public static SnapshotSelectionDto ReadSelection(string json) =>
        JsonSerializer.Deserialize<SnapshotSelectionDto>(json, Options)
        ?? throw new InvalidOperationException("A snapshot's selection is empty.");

    private sealed record WarningDetail(
        string Message, int? PendingConflicts, IReadOnlyList<string>? MissingStateCodes,
        long? ImportId, int? VotesDone, int? VotesTotal);

    public static string WriteWarning(SnapshotWarningDto warning) => JsonSerializer.Serialize(new WarningDetail(
        warning.Message, warning.PendingConflicts, warning.MissingStateCodes,
        warning.ImportId, warning.VotesDone, warning.VotesTotal), Options);

    public static SnapshotWarningDto ReadWarning(SnapshotWarningKind kind, Platform.Contracts.VoteRefDto? vote, string json)
    {
        var detail = JsonSerializer.Deserialize<WarningDetail>(json, Options)
            ?? throw new InvalidOperationException("A snapshot warning has no detail.");
        return new SnapshotWarningDto(kind, detail.Message, vote, detail.PendingConflicts, detail.MissingStateCodes,
            detail.ImportId, detail.VotesDone, detail.VotesTotal);
    }
}
