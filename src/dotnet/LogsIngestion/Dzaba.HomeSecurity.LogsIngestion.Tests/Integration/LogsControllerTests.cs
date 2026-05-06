using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.Org.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

[TestFixture]
public class LogsControllerTests : ControllerTestFixture
{
    [Test]
    public async Task AAA()
    {
        using var scope = CreateScope();
        var orgService = scope.ServiceProvider.GetRequiredService<IOrgService>();
        var newOrgId = await orgService.CreateOrgAsync("Test Org").ConfigureAwait(false);

        var client = CreateClient();

        var requestBody = new IngestLogsRequest
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
        
        var token = JwtTokenBuilder.CreateToken(IssuerSigningKey, Authority, Audience, "1234567890", "John Doe");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/logs")
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
