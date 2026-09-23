using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.LogsIngestion.Service.Messages;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Tests.Controllers;

[TestFixture]
public class LogsControllerTests : LogsIngestionServiceTestFixture
{
    private static IngestLogsRequest CreateSampleRequest() => new()
    {
        Devices =
        [
            new Device_logs
            {
                Device = new Network_device { MacAddress = "AA:BB:CC:DD:EE:FF" },
                Events =
                [
                    new Log_entry
                    {
                        EventId = Guid.NewGuid(),
                        Level = Log_entryLevel.Info,
                        Message = "Device joined the network",
                        Timestamp = DateTimeOffset.UtcNow,
                    },
                ],
            },
        ],
    };

    [Test]
    public async Task Ingest_WhenTokenHasLogsWriteScope_ThenPublishesAndReturnsAccepted()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var client = CreateDeviceClient(tenantId, deviceId);

        var response = await client.PostAsJsonAsync("/api/v1/logs", CreateSampleRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        MessageBus.Published.Should().ContainSingle();
        var (routingKey, message) = MessageBus.Published.Single();
        routingKey.Should().Be(RoutingKeys.LogsIngested);
        var batch = message.Should().BeOfType<LogBatchMessage>().Subject;
        batch.TenantId.Should().Be(tenantId);
        batch.DeviceId.Should().Be(deviceId);
        batch.Devices.Should().ContainSingle();
    }

    [Test]
    public async Task Ingest_WhenTokenLacksLogsWriteScope_ThenReturnsForbidden()
    {
        var client = CreateDeviceClient(Guid.NewGuid(), Guid.NewGuid(), scope: "router:read");

        var response = await client.PostAsJsonAsync("/api/v1/logs", CreateSampleRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        MessageBus.Published.Should().BeEmpty();
    }

    [Test]
    public async Task Ingest_WhenUnauthenticated_ThenReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/logs", CreateSampleRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        MessageBus.Published.Should().BeEmpty();
    }

    [Test]
    public async Task Ingest_WhenDevicesArrayIsEmpty_ThenReturnsBadRequest()
    {
        var client = CreateDeviceClient(Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/v1/logs", new IngestLogsRequest { Devices = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        MessageBus.Published.Should().BeEmpty();
    }
}
