using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Dzaba.HomeSecurity.DomainEvents.Tests;

[TestFixture]
public class DomainEventMessageContractTests
{
    [TestCase("""{"eventId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-09-26T10:00:00+00:00","tenantId":"22222222-2222-2222-2222-222222222222","actor":{"type":"User","id":"a1b2"},"action":"role.assigned","target":{"type":"UserRole","id":"c3d4"},"metadata":{"role":"Admin","grantedTo":"e5f6"}}""")]
    [TestCase("""{"eventId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-09-26T10:00:00+00:00","tenantId":null,"actor":{"type":"System","id":"system"},"action":"identity.login.succeeded","target":{"type":"User","id":"a1b2"},"metadata":{}}""")]
    [TestCase("""{"eventId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-09-26T10:00:00+00:00","tenantId":"22222222-2222-2222-2222-222222222222","actor":{"type":"Device","id":"33333333-3333-3333-3333-333333333333"},"action":"router.credential.fetched","target":{"type":"Router","id":"44444444-4444-4444-4444-444444444444"},"metadata":{"added":["a","b"],"nested":{"n":3}}}""")]
    public void Deserialize_WhenGivenSchemaShapedJson_ThenRoundTripsWithoutLoss(string json)
    {
        var value = JsonSerializer.Deserialize<DomainEventMessage>(json);

        var roundTripped = JsonSerializer.Serialize(value);

        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTripped)).Should().BeTrue();
    }

    [Test]
    public void Deserialize_WhenTheTenantIsNull_ThenTheTenantIdIsNull()
    {
        var json = """{"eventId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-09-26T10:00:00+00:00","tenantId":null,"actor":{"type":"System","id":"system"},"action":"a.b","target":{"type":"T","id":"1"},"metadata":{}}""";

        var value = JsonSerializer.Deserialize<DomainEventMessage>(json);

        value!.TenantId.Should().BeNull();
    }
}
