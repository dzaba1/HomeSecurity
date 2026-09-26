namespace Dzaba.HomeSecurity.DomainEvents;

/// <summary>
/// The envelope's <c>target.type</c> values: what kind of thing an event's
/// operation was done to.
/// </summary>
public static class DomainEventTargetTypes
{
    public const string Organization = "Organization";
    public const string Membership = "Membership";
    public const string UserRole = "UserRole";
    public const string Role = "Role";
    public const string Router = "Router";
    public const string Device = "Device";
}
