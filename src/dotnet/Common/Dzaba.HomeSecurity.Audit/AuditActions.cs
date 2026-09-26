namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// The dotted action names an audit event can carry (the envelope's
/// <c>action</c>), one constant per audited thing, so the literal strings are
/// never duplicated between the publishing service and its tests - see
/// docs/architecture/16-auditing-and-compliance.md section 2. The message is
/// published under <see cref="RoutingKey"/>, e.g. <c>audit.role.assigned</c>,
/// on the shared homesecurity.events exchange.
/// </summary>
public static class AuditActions
{
    public const string RoutingKeyPrefix = "audit.";

    public const string OrganizationCreated = "organization.created";
    public const string MembershipAdded = "membership.added";
    public const string MembershipRemoved = "membership.removed";
    public const string RoleAssigned = "role.assigned";
    public const string RoleUnassigned = "role.unassigned";
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";

    public const string RouterCreated = "router.created";
    public const string RouterUpdated = "router.updated";
    public const string RouterDeleted = "router.deleted";
    public const string RouterCredentialFetched = "router.credential.fetched";
    public const string DeviceAcknowledged = "device.acknowledged";
    public const string DeviceRenamed = "device.renamed";

    public static string RoutingKey(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return RoutingKeyPrefix + action;
    }
}
