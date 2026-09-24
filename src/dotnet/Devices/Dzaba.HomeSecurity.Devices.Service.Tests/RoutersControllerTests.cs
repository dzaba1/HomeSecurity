using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Data.Entities;
using Dzaba.HomeSecurity.Devices.Service.Security;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

[TestFixture]
public sealed class RoutersControllerTests : DevicesServiceTestFixture
{
    private const string Secret = "hunter2-router-password";

    private readonly Guid orgId = Guid.NewGuid();
    private readonly string managerId = Guid.NewGuid().ToString();
    private readonly string viewerId = Guid.NewGuid().ToString();

    [SetUp]
    public void GrantUsers()
    {
        PermissionSource.Grant(orgId, managerId, PermissionKeys.RouterView, PermissionKeys.RouterManage);
        PermissionSource.Grant(orgId, viewerId, PermissionKeys.RouterView);
    }

    private string RoutersUrl(Guid? org = null) => $"/api/v1/orgs/{org ?? orgId}/routers";

    private static object WebScrapeRequest(string name = "Living room") => new
    {
        name,
        host = "192.168.1.1",
        protocol = "WebScrape",
        authMode = "HttpBasic",
        username = "admin",
        secret = Secret,
    };

    private async Task<Guid> CreateRouterAsync(HttpClient client, object? request = null)
    {
        var response = await client.PostAsJsonAsync(RoutersUrl(), request ?? WebScrapeRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ExtractIdAsync(response);
    }

    [Test]
    public async Task Create_WhenValid_ThenCreatedWithHalLinksAndNeverReturnsTheSecret()
    {
        var client = CreateAuthenticatedClient(managerId);

        var response = await client.PostAsJsonAsync(RoutersUrl(), WebScrapeRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Living room");
        body.GetProperty("protocol").GetString().Should().Be("WebScrape");
        body.GetProperty("authMode").GetString().Should().Be("HttpBasic");
        body.TryGetProperty("secret", out _).Should().BeFalse();
        body.GetRawText().Should().NotContain(Secret);
        body.GetProperty("_links").TryGetProperty("edit", out _).Should().BeTrue();
        body.GetProperty("_links").TryGetProperty("delete", out _).Should().BeTrue();
    }

    [Test]
    public async Task Create_WhenValid_ThenTheSecretIsStoredEncryptedAndDecryptsToTheOriginal()
    {
        var routerId = await CreateRouterAsync(CreateAuthenticatedClient(managerId));

        using var db = CreateDbContext(orgId);
        var stored = await db.Routers.SingleAsync(r => r.Id == routerId);
        stored.EncryptedSecret.Should().NotEqual(Encoding.UTF8.GetBytes(Secret));
        Encoding.UTF8.GetString(stored.EncryptedSecret).Should().NotContain(Secret);
        Container.GetRequiredService<IRouterSecretProtector>().Unprotect(orgId, stored.EncryptedSecret).Should().Be(Secret);
    }

    [Test]
    public async Task Create_WhenValid_ThenPublishesRouterChangedCreated()
    {
        var routerId = await CreateRouterAsync(CreateAuthenticatedClient(managerId));

        var published = MessageBus.Published.Should().ContainSingle().Subject;
        published.RoutingKey.Should().Be("router.changed");
        var message = published.Message.Should().BeOfType<RouterChangedMessage>().Subject;
        message.TenantId.Should().Be(orgId);
        message.RouterId.Should().Be(routerId);
        message.Action.Should().Be(RouterChangedMessageAction.Created);
    }

    [Test]
    public async Task Create_WhenSnmpWithoutAuthMode_ThenCreated()
    {
        var client = CreateAuthenticatedClient(managerId);

        var response = await client.PostAsJsonAsync(RoutersUrl(),
            new { name = "Attic", host = "10.0.0.1", protocol = "SNMP", username = "", secret = "public" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("authMode").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestCase("WebScrape", null, "admin", TestName = "Create_WhenWebScrapeHasNoAuthMode_ThenBadRequest")]
    [TestCase("WebScrape", "HttpBasic", "", TestName = "Create_WhenWebScrapeHasNoUsername_ThenBadRequest")]
    [TestCase("SNMP", "HttpBasic", "", TestName = "Create_WhenSnmpHasAnAuthMode_ThenBadRequest")]
    public async Task Create_WhenProtocolAndAuthModeDoNotFit_ThenBadRequest(string protocol, string? authMode, string username)
    {
        var client = CreateAuthenticatedClient(managerId);

        var response = await client.PostAsJsonAsync(RoutersUrl(),
            new { name = "Bad", host = "192.168.1.1", protocol, authMode, username, secret = Secret });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        MessageBus.Published.Should().BeEmpty();
    }

    [Test]
    public async Task Create_WhenSecretIsMissing_ThenBadRequest()
    {
        var client = CreateAuthenticatedClient(managerId);

        var response = await client.PostAsJsonAsync(RoutersUrl(),
            new { name = "NoSecret", host = "192.168.1.1", protocol = "WebScrape", authMode = "HttpBasic", username = "admin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_WhenCallerOnlyHasRouterView_ThenForbidden()
    {
        var response = await CreateAuthenticatedClient(viewerId).PostAsJsonAsync(RoutersUrl(), WebScrapeRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task List_WhenCallerHasNoRouterPermissions_ThenForbidden()
    {
        var nobody = Guid.NewGuid().ToString();
        PermissionSource.Grant(orgId, nobody);

        var response = await CreateAuthenticatedClient(nobody).GetAsync(RoutersUrl());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task List_WhenCallerIsNotAMemberOfTheOrg_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(Guid.NewGuid().ToString()).GetAsync(RoutersUrl());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_WhenUnauthenticated_ThenUnauthorized()
    {
        var response = await CreateClient().GetAsync(RoutersUrl());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task List_WhenRoutersExist_ThenReturnsThemWithACreateLinkOnlyForManagers()
    {
        var manager = CreateAuthenticatedClient(managerId);
        await CreateRouterAsync(manager, WebScrapeRequest("B router"));
        await CreateRouterAsync(manager, WebScrapeRequest("A router"));

        var asManager = await manager.GetFromJsonAsync<JsonElement>(RoutersUrl());
        var asViewer = await CreateAuthenticatedClient(viewerId).GetFromJsonAsync<JsonElement>(RoutersUrl());

        asManager.GetProperty("_embedded").GetProperty("routers").EnumerateArray()
            .Select(r => r.GetProperty("name").GetString()).Should().Equal("A router", "B router");
        asManager.GetProperty("_links").TryGetProperty("create", out _).Should().BeTrue();
        asViewer.GetProperty("_links").TryGetProperty("create", out _).Should().BeFalse();
    }

    [Test]
    public async Task Get_WhenRouterExists_ThenEditAndDeleteLinksAreOnlyForManagers()
    {
        var routerId = await CreateRouterAsync(CreateAuthenticatedClient(managerId));

        var asManager = await CreateAuthenticatedClient(managerId).GetFromJsonAsync<JsonElement>($"{RoutersUrl()}/{routerId}");
        var asViewer = await CreateAuthenticatedClient(viewerId).GetFromJsonAsync<JsonElement>($"{RoutersUrl()}/{routerId}");

        asManager.GetProperty("_links").TryGetProperty("edit", out _).Should().BeTrue();
        asManager.GetProperty("_links").TryGetProperty("delete", out _).Should().BeTrue();
        asViewer.GetProperty("_links").TryGetProperty("edit", out _).Should().BeFalse();
        asViewer.GetProperty("_links").TryGetProperty("delete", out _).Should().BeFalse();
        asViewer.GetRawText().Should().NotContain(Secret);
    }

    [Test]
    public async Task Get_WhenRouterDoesNotExist_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(managerId).GetAsync($"{RoutersUrl()}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Update_WhenSecretIsOmitted_ThenFieldsChangeButTheStoredCiphertextIsUntouched()
    {
        var client = CreateAuthenticatedClient(managerId);
        var routerId = await CreateRouterAsync(client);
        byte[] before;
        using (var db = CreateDbContext(orgId))
        {
            before = (await db.Routers.SingleAsync()).EncryptedSecret;
        }

        var response = await client.PutAsJsonAsync($"{RoutersUrl()}/{routerId}",
            new { name = "Renamed", host = "192.168.1.254", protocol = "WebScrape", authMode = "HttpDigest", username = "root" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verify = CreateDbContext(orgId);
        var stored = await verify.Routers.SingleAsync();
        stored.Name.Should().Be("Renamed");
        stored.Host.Should().Be("192.168.1.254");
        stored.AuthMode.Should().Be(RouterAuthMode.HttpDigest);
        stored.Username.Should().Be("root");
        stored.EncryptedSecret.Should().Equal(before);
    }

    [Test]
    public async Task Update_WhenSecretIsSupplied_ThenTheSecretIsRotated()
    {
        var client = CreateAuthenticatedClient(managerId);
        var routerId = await CreateRouterAsync(client);

        var response = await client.PutAsJsonAsync($"{RoutersUrl()}/{routerId}",
            new { name = "Living room", host = "192.168.1.1", protocol = "WebScrape", authMode = "HttpBasic", username = "admin", secret = "rotated-secret" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("rotated-secret");
        using var db = CreateDbContext(orgId);
        var stored = await db.Routers.SingleAsync();
        Container.GetRequiredService<IRouterSecretProtector>().Unprotect(orgId, stored.EncryptedSecret).Should().Be("rotated-secret");
    }

    [Test]
    public async Task Update_WhenValid_ThenPublishesRouterChangedUpdated()
    {
        var client = CreateAuthenticatedClient(managerId);
        var routerId = await CreateRouterAsync(client);

        await client.PutAsJsonAsync($"{RoutersUrl()}/{routerId}",
            new { name = "Renamed", host = "192.168.1.1", protocol = "WebScrape", authMode = "HttpBasic", username = "admin" });

        MessageBus.Published.Select(p => p.Message).OfType<RouterChangedMessage>()
            .Should().Contain(m => m.RouterId == routerId && m.Action == RouterChangedMessageAction.Updated);
    }

    [Test]
    public async Task Update_WhenRouterDoesNotExist_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(managerId).PutAsJsonAsync($"{RoutersUrl()}/{Guid.NewGuid()}",
            new { name = "Ghost", host = "192.168.1.1", protocol = "WebScrape", authMode = "HttpBasic", username = "admin" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Update_WhenCallerOnlyHasRouterView_ThenForbidden()
    {
        var routerId = await CreateRouterAsync(CreateAuthenticatedClient(managerId));

        var response = await CreateAuthenticatedClient(viewerId).PutAsJsonAsync($"{RoutersUrl()}/{routerId}",
            new { name = "Hacked", host = "6.6.6.6", protocol = "WebScrape", authMode = "HttpBasic", username = "admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Delete_WhenRouterExists_ThenItsBindingsAreRemovedAndDevicesForgetIt()
    {
        var client = CreateAuthenticatedClient(managerId);
        var routerId = await CreateRouterAsync(client);
        var deviceId = Guid.NewGuid();
        using (var db = CreateDbContext(orgId))
        {
            db.AgentRouterBindings.Add(new AgentRouterBinding
            {
                Id = Guid.NewGuid(), TenantId = orgId, DeviceCredentialId = Guid.NewGuid(), RouterId = routerId, CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Devices.Add(new Device
            {
                Id = deviceId, TenantId = orgId, MacAddress = "AA:BB:CC:DD:EE:FF", Status = DeviceStatus.Unknown,
                FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow, LastSeenViaRouterId = routerId,
            });
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"{RoutersUrl()}/{routerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var verify = CreateDbContext(orgId);
        (await verify.Routers.CountAsync()).Should().Be(0);
        (await verify.AgentRouterBindings.CountAsync()).Should().Be(0);
        (await verify.Devices.SingleAsync(d => d.Id == deviceId)).LastSeenViaRouterId.Should().BeNull();
        MessageBus.Published.Select(p => p.Message).OfType<RouterChangedMessage>()
            .Should().Contain(m => m.RouterId == routerId && m.Action == RouterChangedMessageAction.Deleted);
    }

    [Test]
    public async Task Delete_WhenRouterDoesNotExist_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(managerId).DeleteAsync($"{RoutersUrl()}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WhenCallerOnlyHasRouterView_ThenForbidden()
    {
        var routerId = await CreateRouterAsync(CreateAuthenticatedClient(managerId));

        var response = await CreateAuthenticatedClient(viewerId).DeleteAsync($"{RoutersUrl()}/{routerId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Routers_WhenAUserBelongsToTwoOrgs_ThenEachOrgOnlySeesItsOwn()
    {
        var otherOrg = Guid.NewGuid();
        PermissionSource.Grant(otherOrg, managerId, PermissionKeys.RouterView, PermissionKeys.RouterManage);
        var client = CreateAuthenticatedClient(managerId);
        var routerId = await CreateRouterAsync(client);

        var otherList = await client.GetFromJsonAsync<JsonElement>(RoutersUrl(otherOrg));
        var otherGet = await client.GetAsync($"{RoutersUrl(otherOrg)}/{routerId}");
        var otherDelete = await client.DeleteAsync($"{RoutersUrl(otherOrg)}/{routerId}");

        otherList.GetProperty("_embedded").GetProperty("routers").GetArrayLength().Should().Be(0);
        otherGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var db = CreateDbContext(orgId);
        (await db.Routers.CountAsync()).Should().Be(1);
    }
}
