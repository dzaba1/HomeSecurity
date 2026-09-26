using System.Text.Json;
using System.Text.RegularExpressions;
using Dzaba.HomeSecurity.DomainEvents;

namespace Dzaba.HomeSecurity.Audit.Service.Ingestion;

/// <summary>
/// Decides whether a received message is fit to become part of the audit
/// trail. The trail is evidence, so it is strict: a message that doesn't match
/// the envelope contract (domain_event_message.json), or whose name doesn't
/// match the routing key it arrived under, is refused - and dead-lettered by
/// the caller - rather than stored in a shape someone would later have to
/// explain.
/// </summary>
/// <remarks>
/// Hand-written rather than derived from the generated types' annotations:
/// those are regenerated from the schema and can't say most of what matters
/// here (a missing eventId is just <see cref="Guid.Empty"/>, a pattern skips an
/// empty string, the generated defaults make a missing actor look present).
/// The limits below mirror the schema and the column sizes in
/// <c>AuditDbContext</c>.
/// </remarks>
internal static partial class DomainEventValidator
{
    internal const int MaxActionLength = 100;
    internal const int MaxActorIdLength = 200;
    internal const int MaxTargetTypeLength = 100;
    internal const int MaxTargetIdLength = 200;

    // Mirrors "pattern" of "action" in the schema.
    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ActionPattern();

    /// <returns>Why the message is unacceptable, or null when it is fine.</returns>
    public static string? Validate(DomainEventMessage? message, string routingKey)
    {
        if (message is null)
        {
            return "the message body is empty or null";
        }

        if (message.EventId == Guid.Empty)
        {
            return "eventId is missing";
        }

        if (message.OccurredAt == default)
        {
            return "occurredAt is missing";
        }

        // Null means "no tenant". An all-zero id would collide with how the
        // store keys the no-tenant chain, and no service ever has that tenant.
        if (message.TenantId == Guid.Empty)
        {
            return "tenantId is an empty guid (use null for no tenant)";
        }

        if (string.IsNullOrEmpty(message.Action) || message.Action.Length > MaxActionLength || !ActionPattern().IsMatch(message.Action))
        {
            return "action is missing or not a dotted lower-case name";
        }

        // An event is published under its own name; anything else means a
        // publisher or a binding is wrong, and the trail shouldn't guess which.
        if (!string.Equals(message.Action, routingKey, StringComparison.Ordinal))
        {
            return $"action '{message.Action}' does not match the routing key '{routingKey}'";
        }

        if (message.Actor is null || !Enum.IsDefined(message.Actor.Type)
            || string.IsNullOrWhiteSpace(message.Actor.Id) || message.Actor.Id.Length > MaxActorIdLength)
        {
            return "actor is missing or invalid";
        }

        if (message.Target is null
            || string.IsNullOrWhiteSpace(message.Target.Type) || message.Target.Type.Length > MaxTargetTypeLength
            || string.IsNullOrWhiteSpace(message.Target.Id) || message.Target.Id.Length > MaxTargetIdLength)
        {
            return "target is missing or invalid";
        }

        // Off the wire a JSON object is a JsonElement of kind Object; a missing
        // or null metadata is the generated default (a bare object) or null.
        if (message.Metadata is not JsonElement { ValueKind: JsonValueKind.Object })
        {
            return "metadata is missing or not a JSON object";
        }

        return null;
    }
}
