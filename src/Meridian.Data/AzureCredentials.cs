using Azure.Core;
using Azure.Identity;

namespace Meridian.Data;

/// <summary>
/// The one Microsoft Entra ID sign-in the process uses for every Azure service (ADR 0009): <c>az login</c> on a
/// developer's machine, environment variables for a service principal, or a managed identity in Azure. It is built when
/// first asked for, so a run that uses passwords and keys never touches it.
/// </summary>
public static class AzureCredentials
{
    private static readonly Lazy<TokenCredential> Shared = new(() => new DefaultAzureCredential());

    public static TokenCredential Default => Shared.Value;
}
