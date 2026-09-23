using System.Text.Json;
using System.Text.Json.Nodes;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Contracts.Tests;

[TestFixture]
public class ContractRoundTripTests
{
    private const string SampleJson = """
        {
          "devices": [
            {
              "device": { "macAddress": "AA:BB:CC:DD:EE:FF", "ipAddress": "192.168.1.42", "hostname": "laptop" },
              "events": [
                {
                  "eventId": "11111111-1111-1111-1111-111111111111",
                  "level": "info",
                  "message": "Device joined the network",
                  "timestamp": "2026-09-23T10:00:00+00:00"
                }
              ]
            }
          ]
        }
        """;

    [Test]
    public void Deserialize_WhenGivenSchemaShapedJson_ThenRoundTripsWithoutLoss()
    {
        var value = JsonSerializer.Deserialize<IngestLogsRequest>(SampleJson);

        var roundTripped = JsonSerializer.Serialize(value);

        JsonNode.DeepEquals(JsonNode.Parse(SampleJson), JsonNode.Parse(roundTripped)).Should().BeTrue();
    }

    [Test]
    public void Deserialize_WhenDeviceHasOnlyRequiredFields_ThenOptionalFieldsAreNull()
    {
        const string json = """
            {
              "devices": [
                {
                  "device": { "macAddress": "AA:BB:CC:DD:EE:FF" },
                  "events": [
                    { "eventId": "11111111-1111-1111-1111-111111111111", "level": "debug", "message": "poll", "timestamp": "2026-09-23T10:00:00+00:00" }
                  ]
                }
              ]
            }
            """;

        var value = JsonSerializer.Deserialize<IngestLogsRequest>(json);

        value!.Devices.Single().Device.IpAddress.Should().BeNull();
        value.Devices.Single().Device.Hostname.Should().BeNull();
    }
}
