using Microsoft.AspNetCore.Authorization;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;

/// <summary>
/// Requires the device JWT's space-delimited "scope" claim to contain a
/// specific scope token (e.g. "logs:write") - the device-auth equivalent of
/// Org's permission-key requirement, checked against the JWT itself rather
/// than a DB-backed permission catalog since devices aren't tenant users.
/// </summary>
internal sealed class ScopeRequirement : IAuthorizationRequirement
{
    public string RequiredScope { get; }

    public ScopeRequirement(string requiredScope)
    {
        ArgumentException.ThrowIfNullOrEmpty(requiredScope);

        RequiredScope = requiredScope;
    }
}
