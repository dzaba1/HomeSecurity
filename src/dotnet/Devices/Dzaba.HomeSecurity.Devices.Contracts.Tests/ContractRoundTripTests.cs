using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Devices.Contracts.Tests;

[TestFixture]
public class ContractRoundTripTests
{
    [TestCase(typeof(Router), """{"id":"11111111-1111-1111-1111-111111111111","name":"Living room","host":"192.168.1.1","protocol":"WebScrape","authMode":"HttpBasic","username":"admin","updatedAt":"2026-09-24T10:00:00+00:00"}""")]
    [TestCase(typeof(Router), """{"id":"11111111-1111-1111-1111-111111111111","name":"Attic","host":"10.0.0.1","protocol":"SNMP","authMode":null,"username":"","updatedAt":"2026-09-24T10:00:00+00:00"}""")]
    [TestCase(typeof(CreateRouter), """{"name":"Living room","host":"192.168.1.1","protocol":"WebScrape","authMode":"FormLogin","username":"admin","secret":"hunter2"}""")]
    [TestCase(typeof(CreateRouter), """{"name":"Attic","host":"10.0.0.1","protocol":"SNMP","authMode":null,"username":"","secret":"public"}""")]
    [TestCase(typeof(UpdateRouter), """{"name":"Living room","host":"192.168.1.1","protocol":"WebScrape","authMode":"HttpDigest","username":"admin","secret":"rotated"}""")]
    [TestCase(typeof(UpdateRouter), """{"name":"Living room","host":"192.168.1.1","protocol":"WebScrape","authMode":"HttpBasic","username":"admin","secret":null}""")]
    [TestCase(typeof(RouterChangedMessage), """{"tenantId":"11111111-1111-1111-1111-111111111111","routerId":"22222222-2222-2222-2222-222222222222","action":"Created","changedAt":"2026-09-24T10:00:00+00:00"}""")]
    public void Deserialize_WhenGivenSchemaShapedJson_ThenRoundTripsWithoutLoss(Type contractType, string json)
    {
        var value = JsonSerializer.Deserialize(json, contractType);

        var roundTripped = JsonSerializer.Serialize(value, contractType);

        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTripped)).Should().BeTrue();
    }

    [Test]
    public void Deserialize_WhenAuthModeIsOmitted_ThenItIsNullNotTheFirstEnumValue()
    {
        var request = JsonSerializer.Deserialize<CreateRouter>(
            """{"name":"Attic","host":"10.0.0.1","protocol":"SNMP","username":"","secret":"public"}""");

        request!.AuthMode.Should().BeNull();
    }
}
