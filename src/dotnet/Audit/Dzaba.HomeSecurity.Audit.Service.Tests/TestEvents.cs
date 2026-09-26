using System.Text;
using System.Text.Json;
using Dzaba.HomeSecurity.DomainEvents;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

/// <summary>
/// Builds domain events the way they reach the service - as JSON off the wire -
/// so a test's <c>Metadata</c> is the <see cref="JsonElement"/> production sees,
/// not a dictionary a publisher happened to hold.
/// </summary>
internal static class TestEvents
{
    public static string Json(
        Guid? eventId = null,
        string action = "role.assigned",
        Guid? tenantId = null,
        string actorType = "User",
        string actorId = "user-1",
        string targetType = "UserRole",
        string targetId = "user-2:role-1",
        string metadata = """{"roleName":"Admin"}""",
        DateTimeOffset? occurredAt = null) =>
        $$"""
        {
          "eventId": "{{eventId ?? Guid.NewGuid()}}",
          "occurredAt": "{{(occurredAt ?? new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)).ToString("O")}}",
          "tenantId": {{(tenantId is null ? "null" : $"\"{tenantId}\"")}},
          "actor": { "type": "{{actorType}}", "id": "{{actorId}}" },
          "action": "{{action}}",
          "target": { "type": "{{targetType}}", "id": "{{targetId}}" },
          "metadata": {{metadata}}
        }
        """;

    public static byte[] Body(string json) => Encoding.UTF8.GetBytes(json);

    public static DomainEventMessage Message(
        Guid? eventId = null,
        string action = "role.assigned",
        Guid? tenantId = null,
        string actorType = "User",
        string actorId = "user-1",
        string targetType = "UserRole",
        string targetId = "user-2:role-1",
        string metadata = """{"roleName":"Admin"}""",
        DateTimeOffset? occurredAt = null) =>
        JsonSerializer.Deserialize<DomainEventMessage>(
            Json(eventId, action, tenantId, actorType, actorId, targetType, targetId, metadata, occurredAt))!;
}
