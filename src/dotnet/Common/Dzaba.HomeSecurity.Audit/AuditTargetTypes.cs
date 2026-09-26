namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// The envelope's <c>target.type</c> values: what kind of thing an audited
/// action was done to.
/// </summary>
public static class AuditTargetTypes
{
    public const string Organization = "Organization";
    public const string Membership = "Membership";
    public const string UserRole = "UserRole";
    public const string Role = "Role";
    public const string Router = "Router";
    public const string Device = "Device";
}
