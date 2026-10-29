namespace Meridian.Extractions;

/// <summary>
/// <c>DocumentIntelligence:*</c>. The endpoint and key are secrets that belong in <c>.env</c> (compose) or user-secrets,
/// never in the repository; the rest are defaults.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    /// <summary>The Azure AI Document Intelligence resource, such as <c>https://name.cognitiveservices.azure.com/</c>.</summary>
    public string? Endpoint { get; set; }

    public string? Key { get; set; }

    /// <summary>The layout model returns tables as cells with text; the mapper decides what the marks mean (ADR 0008).</summary>
    public string ModelId { get; set; } = "prebuilt-layout";

    public string ApiVersion { get; set; } = "2024-11-30";

    /// <summary>How long to wait between asking whether the analysis has finished.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Key);
}
