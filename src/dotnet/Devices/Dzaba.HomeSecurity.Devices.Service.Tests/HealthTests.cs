using System.Net;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

/// <summary>
/// Smoke test that the host builds (DI validation runs in Development) and
/// serves - /health/ready is deliberately not exercised, since it needs a
/// reachable Postgres.
/// </summary>
[TestFixture]
public sealed class HealthTests : DevicesServiceTestFixture
{
    [Test]
    public async Task Live_WhenCalled_ThenOk()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
