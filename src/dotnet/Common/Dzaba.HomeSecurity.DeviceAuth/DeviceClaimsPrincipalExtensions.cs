using System.Security.Claims;

namespace Dzaba.HomeSecurity.DeviceAuth;

public static class DeviceClaimsPrincipalExtensions
{
    /// <summary>
    /// The tenant this device belongs to, trusted directly from the
    /// validated device JWT - never a client-supplied value. See
    /// docs/architecture/02-multi-tenancy.md's machine-to-machine carve-out.
    /// </summary>
    public static Guid GetTenantId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirst(DeviceClaimTypes.TenantId)?.Value;
        return Guid.Parse(value ?? throw new InvalidOperationException($"Missing '{DeviceClaimTypes.TenantId}' claim."));
    }

    public static Guid GetDeviceId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirst(DeviceClaimTypes.DeviceId)?.Value;
        return Guid.Parse(value ?? throw new InvalidOperationException($"Missing '{DeviceClaimTypes.DeviceId}' claim."));
    }
}
