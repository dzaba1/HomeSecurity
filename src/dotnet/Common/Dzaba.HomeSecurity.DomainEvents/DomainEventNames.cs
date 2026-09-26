namespace Dzaba.HomeSecurity.DomainEvents;

/// <summary>
/// The dotted names of the domain events services publish (the envelope's
/// <c>action</c>), one constant per operation, so the literal strings are
/// never duplicated between the publishing service, its subscribers and their
/// tests. A name is also the routing key the event is published under on the
/// shared homesecurity.events exchange - see
/// docs/architecture/16-auditing-and-compliance.md.
/// </summary>
public static class DomainEventNames
{
    // Org.Service
    public const string OrganizationCreated = "organization.created";
    public const string MembershipAdded = "membership.added";
    public const string MembershipRemoved = "membership.removed";
    public const string RoleAssigned = "role.assigned";
    public const string RoleUnassigned = "role.unassigned";
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";

    // Devices.Service
    public const string RouterCreated = "router.created";
    public const string RouterUpdated = "router.updated";
    public const string RouterDeleted = "router.deleted";
    public const string RouterCredentialFetched = "router.credential.fetched";
    public const string DeviceUpdated = "device.updated";
}
