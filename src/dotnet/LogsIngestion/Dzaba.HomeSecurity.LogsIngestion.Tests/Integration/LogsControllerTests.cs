using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using FluentAssertions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

[TestFixture]
public class LogsControllerTests : ControllerTestFixture
{
    [Test]
    public async Task AAA()
    {
        var client = CreateClient();

        var request = new IngestLogsRequest
        {
            Events = [
                new Events
                    {
                        EventId = Guid.NewGuid(),
                        DeviceId = 2,
                        Level = EventsLevel.Info,
                        Message = "Test message"
                    }
                ]
        };

        var resp = await client.PostAsJsonAsync("/api/v1/logs", request);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
