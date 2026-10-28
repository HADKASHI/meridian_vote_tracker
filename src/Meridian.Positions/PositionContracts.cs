using System.ComponentModel.DataAnnotations;
using Meridian.Domain;
using Meridian.Platform.Contracts;

namespace Meridian.Positions;

public sealed record PositionWorklistItemDto(VoteRefDto Vote, int Session, DateOnly VoteDate, string Title, PositionDto? Position);

/// <summary>Set a vote's stance and weight, conditional on the position the editor saw.</summary>
public sealed class SetPositionRequest
{
    [Required] public Stance? Stance { get; init; }
    [Required, Range(1, 5)] public short? Weight { get; init; }

    /// <summary>The revision id of the position the editor saw; null when it had none.</summary>
    public long? SeenRevisionId { get; init; }
}

/// <summary><c>stale-position</c> (409): the position changed while the editor worked; the current one.</summary>
public sealed class StalePositionProblem : Problem
{
    public required PositionDto? Current { get; init; }
}
