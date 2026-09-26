using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using DeviceEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.Device;
using DeviceStatusEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.DeviceStatus;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

[TestFixture]
public sealed class DevicesControllerTests : DevicesServiceTestFixture
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly string managerId = Guid.NewGuid().ToString();
    private readonly string viewerId = Guid.NewGuid().ToString();

    [SetUp]
    public void GrantUsers()
    {
        PermissionSource.Grant(orgId, managerId, PermissionKeys.DeviceView, PermissionKeys.DeviceManage);
        PermissionSource.Grant(orgId, viewerId, PermissionKeys.DeviceView);
    }

    private string DevicesUrl(Guid? org = null) => $"/api/v1/orgs/{org ?? orgId}/devices";

    private async Task<Guid> SeedDeviceAsync(string mac = "AA:BB:CC:DD:EE:FF", Guid? tenant = null,
        DateTimeOffset? lastSeenAt = null, Guid? viaRouterId = null)
    {
        var id = Guid.NewGuid();
        var seen = lastSeenAt ?? DateTimeOffset.UtcNow;
        using var db = CreateDbContext(tenant ?? orgId);
        db.Devices.Add(new DeviceEntity
        {
            Id = id,
            TenantId = tenant ?? orgId,
            MacAddress = mac,
            Status = DeviceStatusEntity.Unknown,
            FirstSeenAt = seen,
            LastSeenAt = seen,
            LastSeenViaRouterId = viaRouterId,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Test]
    public async Task List_WhenDevicesExist_ThenReturnsThemMostRecentlySeenFirstWithoutAnEditLink()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedDeviceAsync("AA:AA:AA:AA:AA:01", lastSeenAt: now.AddHours(-2));
        await SeedDeviceAsync("AA:AA:AA:AA:AA:02", lastSeenAt: now);
        await SeedDeviceAsync("AA:AA:AA:AA:AA:03", lastSeenAt: now.AddHours(-1));

        var body = await CreateAuthenticatedClient(viewerId).GetFromJsonAsync<JsonElement>(DevicesUrl());

        body.GetProperty("_embedded").GetProperty("devices").EnumerateArray()
            .Select(d => d.GetProperty("macAddress").GetString())
            .Should().Equal("AA:AA:AA:AA:AA:02", "AA:AA:AA:AA:AA:03", "AA:AA:AA:AA:AA:01");
        body.GetProperty("_links").TryGetProperty("create", out _).Should().BeFalse();
    }

    [Test]
    public async Task List_WhenCallerHasNoDevicePermissions_ThenForbidden()
    {
        var nobody = Guid.NewGuid().ToString();
        PermissionSource.Grant(orgId, nobody);

        var response = await CreateAuthenticatedClient(nobody).GetAsync(DevicesUrl());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task List_WhenCallerIsNotAMemberOfTheOrg_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(Guid.NewGuid().ToString()).GetAsync(DevicesUrl());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_WhenUnauthenticated_ThenUnauthorized()
    {
        var response = await CreateClient().GetAsync(DevicesUrl());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Get_WhenDeviceExists_ThenTheEditLinkIsOnlyForManagers()
    {
        var routerId = Guid.NewGuid();
        var deviceId = await SeedDeviceAsync(viaRouterId: routerId);

        var asManager = await CreateAuthenticatedClient(managerId).GetFromJsonAsync<JsonElement>($"{DevicesUrl()}/{deviceId}");
        var asViewer = await CreateAuthenticatedClient(viewerId).GetFromJsonAsync<JsonElement>($"{DevicesUrl()}/{deviceId}");

        asManager.GetProperty("macAddress").GetString().Should().Be("AA:BB:CC:DD:EE:FF");
        asManager.GetProperty("status").GetString().Should().Be("Unknown");
        asManager.GetProperty("lastSeenViaRouterId").GetGuid().Should().Be(routerId);
        asManager.GetProperty("_links").TryGetProperty("edit", out _).Should().BeTrue();
        asViewer.GetProperty("_links").TryGetProperty("edit", out _).Should().BeFalse();
    }

    [Test]
    public async Task Get_WhenDeviceDoesNotExist_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(viewerId).GetAsync($"{DevicesUrl()}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_WhenMarkedKnown_ThenStatusChangesAndDeviceUpdatedIsPublished()
    {
        var deviceId = await SeedDeviceAsync();

        var response = await CreateAuthenticatedClient(managerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { status = "Known" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Known");
        using var db = CreateDbContext(orgId);
        (await db.Devices.SingleAsync()).Status.Should().Be(DeviceStatusEntity.Known);

        var published = MessageBus.Published.Should().ContainSingle().Subject;
        published.RoutingKey.Should().Be("device.updated");
        var message = published.Message.Should().BeOfType<DomainEventMessage>().Subject;
        message.TenantId.Should().Be(orgId);
        message.Actor.Id.Should().Be(managerId);
        message.Target.Type.Should().Be(DomainEventTargetTypes.Device);
        message.Target.Id.Should().Be(deviceId.ToString());
        var changes = (IReadOnlyDictionary<string, object?>)((IReadOnlyDictionary<string, object?>)message.Metadata)["changes"]!;
        changes.Keys.Should().BeEquivalentTo("status");
        var status = (IReadOnlyDictionary<string, object?>)changes["status"]!;
        status["from"].Should().Be("Unknown");
        status["to"].Should().Be("Known");
        // The MAC address is personal data: the target id identifies the device.
        JsonSerializer.Serialize(message).Should().NotContain("AA:BB:CC:DD:EE:FF");
    }

    [Test]
    public async Task Patch_WhenRenamedAndMarkedKnownTogether_ThenExactlyOneDeviceUpdatedEventCarriesBothChanges()
    {
        var deviceId = await SeedDeviceAsync();

        await CreateAuthenticatedClient(managerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { name = "Dad's phone", status = "Known" });

        var message = MessageBus.Published.Should().ContainSingle().Subject.Message
            .Should().BeOfType<DomainEventMessage>().Subject;
        var changes = (IReadOnlyDictionary<string, object?>)((IReadOnlyDictionary<string, object?>)message.Metadata)["changes"]!;
        changes.Keys.Should().BeEquivalentTo("name", "status");
        ((IReadOnlyDictionary<string, object?>)changes["name"]!)["to"].Should().Be("Dad's phone");
    }

    [Test]
    public async Task Patch_WhenOnlyRenamed_ThenTheStatusIsLeftAlone()
    {
        var deviceId = await SeedDeviceAsync();

        var response = await CreateAuthenticatedClient(managerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { name = "  Dad's phone  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var db = CreateDbContext(orgId);
        var stored = await db.Devices.SingleAsync();
        stored.Name.Should().Be("Dad's phone");
        stored.Status.Should().Be(DeviceStatusEntity.Unknown);
    }

    [Test]
    public async Task Patch_WhenNameIsEmpty_ThenTheNameIsCleared()
    {
        var deviceId = await SeedDeviceAsync();
        var client = CreateAuthenticatedClient(managerId);
        await client.PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { name = "Phone" });

        var response = await client.PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var db = CreateDbContext(orgId);
        (await db.Devices.SingleAsync()).Name.Should().BeNull();
    }

    [Test]
    public async Task Patch_WhenNoFieldIsSupplied_ThenBadRequestAndNothingIsPublished()
    {
        var deviceId = await SeedDeviceAsync();

        var response = await CreateAuthenticatedClient(managerId).PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        MessageBus.Published.Should().BeEmpty();
    }

    [Test]
    public async Task Patch_WhenStatusIsNotAKnownValue_ThenBadRequest()
    {
        var deviceId = await SeedDeviceAsync();

        var response = await CreateAuthenticatedClient(managerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { status = "Trusted" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Patch_WhenDeviceDoesNotExist_ThenNotFound()
    {
        var response = await CreateAuthenticatedClient(managerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{Guid.NewGuid()}", new { status = "Known" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_WhenCallerOnlyHasDeviceView_ThenForbidden()
    {
        var deviceId = await SeedDeviceAsync();

        var response = await CreateAuthenticatedClient(viewerId)
            .PatchAsJsonAsync($"{DevicesUrl()}/{deviceId}", new { status = "Known" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var db = CreateDbContext(orgId);
        (await db.Devices.SingleAsync()).Status.Should().Be(DeviceStatusEntity.Unknown);
    }

    [Test]
    public async Task Devices_WhenAUserBelongsToTwoOrgs_ThenEachOrgOnlySeesItsOwn()
    {
        var otherOrg = Guid.NewGuid();
        PermissionSource.Grant(otherOrg, managerId, PermissionKeys.DeviceView, PermissionKeys.DeviceManage);
        var deviceId = await SeedDeviceAsync();
        var client = CreateAuthenticatedClient(managerId);

        var otherList = await client.GetFromJsonAsync<JsonElement>(DevicesUrl(otherOrg));
        var otherGet = await client.GetAsync($"{DevicesUrl(otherOrg)}/{deviceId}");
        var otherPatch = await client.PatchAsJsonAsync($"{DevicesUrl(otherOrg)}/{deviceId}", new { status = "Known" });

        otherList.GetProperty("_embedded").GetProperty("devices").GetArrayLength().Should().Be(0);
        otherGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherPatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var db = CreateDbContext(orgId);
        (await db.Devices.SingleAsync()).Status.Should().Be(DeviceStatusEntity.Unknown);
    }
}
