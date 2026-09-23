using Microsoft.AspNetCore.Authorization;

namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// One requirement per catalog permission key - the policy name and the
/// requirement carry the same key, see Bootstrapper's policy registration
/// over <c>PermissionKeys.All</c>.
/// </summary>
internal sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }

    public PermissionRequirement(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionKey);

        PermissionKey = permissionKey;
    }
}
