namespace Dzaba.HomeSecurity.Devices.Service.Authorization;

/// <summary>
/// What distinguishes the agent-facing endpoints from the admin ones: they are
/// authenticated with a device token under its own scheme, since the default
/// scheme here validates Keycloak-issued human tokens.
/// </summary>
internal static class AgentAuth
{
    public const string DeviceTokenScheme = "DeviceToken";

    /// <summary>The scope a device token needs to read its router's connection settings.</summary>
    public const string RouterReadScope = "router:read";
}
