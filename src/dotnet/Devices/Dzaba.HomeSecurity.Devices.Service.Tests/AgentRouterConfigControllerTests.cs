using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.HomeSecurity.Devices.Service.Authorization;
using Dzaba.HomeSecurity.Devices.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

[TestFixture]
public sealed class AgentRouterConfigControllerTests : DevicesServiceTestFixture
{
    private const string Secret = "hunter2-router-password";

    private readonly Guid orgId = Guid.NewGuid();
    private readonly Guid agentId = Guid.NewGuid();
    private readonly string managerId = Guid.NewGuid().ToString();

    [SetUp]
    public void GrantManager()
    {
        PermissionSource.Grant(orgId, managerId, PermissionKeys.RouterView, PermissionKeys.RouterManage);
    }

    private static string ConfigUrl(Guid deviceId) => $"/api/v1/devices/{deviceId}/router-config";

    // Created through the admin API, so the secret reaches storage through the
    // real encryption path the agent endpoint has to reverse.
    private async Task<Guid> CreateRouterAsync(Guid? tenant = null, string host = "192.168.1.1")
    {
        var tenantId = tenant ?? orgId;
        var manager = tenant is null ? managerId : Guid.NewGuid().ToString();
        if (tenant is not null)
        {
            PermissionSource.Grant(tenantId, manager, PermissionKeys.RouterView, PermissionKeys.RouterManage);
        }

        var response = await CreateAuthenticatedClient(manager).PostAsJsonAsync($"/api/v1/orgs/{tenantId}/routers",
            new { name = "Living room", host, protocol = "WebScrape", authMode = "HttpBasic", username = "admin", secret = Secret });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ExtractIdAsync(response);
    }

    private async Task BindAsync(Guid tenantId, Guid deviceCredentialId, Guid routerId)
    {
        using var db = CreateDbContext(tenantId);
        db.AgentRouterBindings.Add(new AgentRouterBinding
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DeviceCredentialId = deviceCredentialId, RouterId = routerId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Get_WhenTheAgentIsPairedWithARouter_ThenReturnsItsSettingsWithTheDecryptedSecret()
    {
        var routerId = await CreateRouterAsync();
        await BindAsync(orgId, agentId, routerId);

        var response = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("routerId").GetGuid().Should().Be(routerId);
        body.GetProperty("host").GetString().Should().Be("192.168.1.1");
        body.GetProperty("protocol").GetString().Should().Be("WebScrape");
        body.GetProperty("authMode").GetString().Should().Be("HttpBasic");
        body.GetProperty("username").GetString().Should().Be("admin");
        body.GetProperty("secret").GetString().Should().Be(Secret);
        body.TryGetProperty("_links", out _).Should().BeFalse("the agent API is plain JSON, not HAL");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Test]
    public async Task Get_WhenTheAgentIsHandedItsConfig_ThenPublishesRouterCredentialFetchedByThatDeviceWithNoSecret()
    {
        var routerId = await CreateRouterAsync();
        await BindAsync(orgId, agentId, routerId);

        await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(agentId));

        var message = MessageBus.Published.Single(p => p.RoutingKey == "router.credential.fetched").Message
            .Should().BeOfType<DomainEventMessage>().Subject;
        message.TenantId.Should().Be(orgId, "the tenant comes from the device token, not an {orgId} route segment");
        message.Actor.Type.Should().Be(ActorType.Device);
        message.Actor.Id.Should().Be(agentId.ToString());
        message.Target.Type.Should().Be(DomainEventTargetTypes.Router);
        message.Target.Id.Should().Be(routerId.ToString());
        ((IReadOnlyDictionary<string, object?>)message.Metadata)["deviceCredentialId"].Should().Be(agentId);
        JsonSerializer.Serialize(message).Should().NotContain(Secret);
    }

    [Test]
    public async Task Get_WhenNoCredentialIsHandedOut_ThenNothingIsPublished()
    {
        var response = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        MessageBus.Published.Should().NotContain(p => p.RoutingKey == "router.credential.fetched");
    }

    [Test]
    public async Task Get_WhenTheRouterSecretIsRotated_ThenTheAgentSeesTheNewOne()
    {
        var routerId = await CreateRouterAsync();
        await BindAsync(orgId, agentId, routerId);
        await CreateAuthenticatedClient(managerId).PutAsJsonAsync($"/api/v1/orgs/{orgId}/routers/{routerId}",
            new { name = "Living room", host = "192.168.1.1", protocol = "WebScrape", authMode = "HttpBasic", username = "admin", secret = "rotated" });

        var body = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope)
            .GetFromJsonAsync<JsonElement>(ConfigUrl(agentId));

        body.GetProperty("secret").GetString().Should().Be("rotated");
    }

    [Test]
    public async Task Get_WhenTheAgentHasNoRouterPaired_ThenNotFound()
    {
        var response = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenTheRouterBelongsToAnotherTenant_ThenNotFoundEvenIfTheCredentialIdMatches()
    {
        var otherOrg = Guid.NewGuid();
        var otherRouterId = await CreateRouterAsync(otherOrg);
        await BindAsync(otherOrg, agentId, otherRouterId);

        // Same credential id, but the token says this agent belongs to orgId.
        var response = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_WhenTheRouteNamesAnotherDevice_ThenForbidden()
    {
        var routerId = await CreateRouterAsync();
        var otherAgentId = Guid.NewGuid();
        await BindAsync(orgId, otherAgentId, routerId);

        var response = await CreateDeviceClient(orgId, agentId, AgentAuth.RouterReadScope).GetAsync(ConfigUrl(otherAgentId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Get_WhenTheTokenLacksTheRouterReadScope_ThenForbidden()
    {
        var routerId = await CreateRouterAsync();
        await BindAsync(orgId, agentId, routerId);

        var response = await CreateDeviceClient(orgId, agentId, "logs:write").GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Get_WhenUnauthenticated_ThenUnauthorized()
    {
        var response = await CreateClient().GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Get_WhenCalledWithAHumanToken_ThenForbidden()
    {
        // A signed token with no device claims or scope is not a device token.
        var response = await CreateAuthenticatedClient(managerId).GetAsync(ConfigUrl(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
