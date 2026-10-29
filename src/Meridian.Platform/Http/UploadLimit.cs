using Microsoft.AspNetCore.Http.Metadata;

namespace Meridian.Platform.Http;

/// <summary>An upload endpoint's request size limit (<c>Imports:MaxUploadBytes</c>, <c>Extractions:MaxUploadBytes</c>).</summary>
public sealed class UploadLimit(long maxBytes) : IRequestSizeLimitMetadata
{
    public long? MaxRequestBodySize => maxBytes;
}
