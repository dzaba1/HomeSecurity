using System.Reflection;
using Dzaba.HomeSecurity.DomainEvents;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

namespace Dzaba.HomeSecurity.Audit.Service.Ingestion;

/// <summary>
/// What Audit.Service listens for: the queue it owns, and which events are
/// bound to it. The audit trail records every domain event the services
/// publish, so every name in <see cref="DomainEventNames"/> is bound
/// automatically (adding an event there adds it to the trail), plus the whole
/// <c>identity.#</c> family the Keycloak webhook will publish.
/// </summary>
internal static class AuditSubscription
{
    public const string Queue = "homesecurity.audit-service";

    private const string IdentityEvents = "identity.#";

    public static SubscriptionOptions CreateOptions() => new()
    {
        Queue = Queue,
        RoutingKeys = RoutingKeys(),
    };

    public static IReadOnlyList<string> RoutingKeys() =>
    [
        .. typeof(DomainEventNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!),
        IdentityEvents,
    ];
}
