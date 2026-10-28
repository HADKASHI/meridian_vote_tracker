namespace Meridian.Platform.Contracts;

// Schemas of the ProblemDetails bodies, for the OpenAPI document. At run time problems
// are written as ProblemDetails with these extension members, so the shape is the same.

/// <summary>Every error: a fixed <c>type</c> from the catalogue, and the trace id to quote.</summary>
public class Problem
{
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required int Status { get; init; }
    public string? Detail { get; init; }
    public required string TraceId { get; init; }
}

/// <summary><c>validation</c> (400): messages keyed by camelCase JSON path.</summary>
public sealed class ValidationProblem : Problem
{
    public required IReadOnlyDictionary<string, string[]> Errors { get; init; }
}
