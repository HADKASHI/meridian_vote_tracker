namespace Meridian.Platform.Problems;

/// <summary>
/// The fixed catalogue of ProblemDetails <c>type</c> values. The UI switches on these one
/// to one; every problem also carries <c>traceId</c>.
/// </summary>
public static class ProblemTypes
{
    public const string Validation = "validation";
    public const string BadRequest = "bad-request";
    public const string NoVotesMatch = "no-votes-match";
    public const string NoPosition = "no-position";
    public const string UnknownUser = "unknown-user";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not-found";
    public const string MethodNotAllowed = "method-not-allowed";
    public const string AlreadyDecided = "already-decided";
    public const string NotRetryable = "not-retryable";
    public const string StalePosition = "stale-position";
    public const string QuarterPublished = "quarter-published";
    public const string PayloadTooLarge = "payload-too-large";
    public const string Unexpected = "unexpected";
    public const string NotImplemented = "not-implemented";

    /// <summary>The type for a framework-generated problem that only has a status code.</summary>
    public static string ForStatus(int status, bool hasFieldErrors) => status switch
    {
        400 => hasFieldErrors ? Validation : BadRequest,
        401 => UnknownUser,
        403 => Forbidden,
        404 => NotFound,
        405 => MethodNotAllowed,
        413 => PayloadTooLarge,
        501 => NotImplemented,
        _ => Unexpected,
    };
}
