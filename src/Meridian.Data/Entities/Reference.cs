using Meridian.Domain;

namespace Meridian.Data.Entities;

public sealed class MemberState
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}

public sealed class MembershipPeriod
{
    public long Id { get; init; }
    public required string StateCode { get; init; }
    public DateOnly MemberFrom { get; init; }
    /// <summary>Null means still a member.</summary>
    public DateOnly? MemberTo { get; init; }
}

public sealed class AppUser
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public Role Role { get; init; }
}
