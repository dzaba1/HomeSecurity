namespace Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;

/// <summary>
/// Claim names carried by a device JWT, per docs/architecture/03-security-and-identity.md.
/// </summary>
internal static class DeviceClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string DeviceId = "device_id";
    public const string Scope = "scope";
}
