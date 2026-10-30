using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meridian.Extractions;

/// <summary>How a sheet and its check are stored in the extraction's JSON columns: stable names, enums as words.</summary>
internal static class ExtractionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Read<T>(string? json) where T : class => json is null ? null : JsonSerializer.Deserialize<T>(json, Options);
}
