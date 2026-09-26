namespace Dzaba.HomeSecurity.DomainEvents;

/// <summary>
/// Who is performing the current action, for the event envelope's
/// <c>actor</c>. Injected into services instead of threading a user id
/// through every method: a human (Keycloak subject), a device (agent
/// credential id from its token) or the system itself (no authenticated
/// request, e.g. a background job).
/// </summary>
public interface ICurrentActor
{
    Actor GetCurrent();
}
