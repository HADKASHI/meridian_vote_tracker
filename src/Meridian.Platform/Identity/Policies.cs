namespace Meridian.Platform.Identity;

/// <summary>The two authorization policies. Admin is a superset of Analyst.</summary>
public static class Policies
{
    /// <summary>Analysts and the admin: view, upload, decide conflicts, create snapshots.</summary>
    public const string Analyst = "Analyst";

    /// <summary>The admin only: set positions and weights, publish snapshots.</summary>
    public const string Admin = "Admin";
}
