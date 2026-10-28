using System.Text.Json;
using Meridian.Data;
using Meridian.Data.Entities;
using Meridian.Platform.Contracts;

namespace Meridian.Imports;

/// <summary>A warning's <c>detail</c> JSON: its message plus the facts of its kind.</summary>
internal sealed record ImportWarningDetail(
    string Message,
    IReadOnlyList<string>? Columns = null,
    long? IdenticalToImportId = null,
    IReadOnlyList<string>? MissingStateCodes = null,
    DateTimeOffset? NewestPublishedAt = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public ImportWarning ToWarning(long importId, ImportWarningKind kind, long? voteId = null) => new()
    {
        ImportId = importId, Kind = SnakeCaseEnumConverter<ImportWarningKind>.ToName(kind), VoteId = voteId,
        Detail = JsonSerializer.Serialize(this, Json),
    };

    public static ImportWarningDto ToDto(ImportWarning warning, VoteRefDto? vote)
    {
        var detail = JsonSerializer.Deserialize<ImportWarningDetail>(warning.Detail, Json)!;
        return new ImportWarningDto(warning.Id, SnakeCaseEnumConverter<ImportWarningKind>.FromName(warning.Kind),
            detail.Message, vote, detail.IdenticalToImportId, detail.Columns, detail.MissingStateCodes, detail.NewestPublishedAt);
    }
}
