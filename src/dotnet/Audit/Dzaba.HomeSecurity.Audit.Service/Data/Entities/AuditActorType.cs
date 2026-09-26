namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// Who performed an audited action: a person (Keycloak subject), an agent
/// (device credential id) or the system itself. Mirrors the event envelope's
/// <c>actor.type</c>; kept separate so the stored model doesn't depend on the
/// wire contract's generated types.
/// </summary>
internal enum AuditActorType
{
    User,
    Device,
    System
}
