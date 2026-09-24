using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

/// <summary>
/// Devices.Service answers "is this user a member / do they hold this
/// permission" from a shared cache and, on a miss, from Org.Service (ADR-0016).
/// These tests pin down the accepted trade-off of that choice: a warm cache
/// keeps the admin API working through an Org.Service outage, a cold one
/// fails loudly - never with a wrong 403/404 that would look like a real
/// answer.
/// </summary>
[TestFixture]
public sealed class OrgOutageTests : DevicesServiceTestFixture
{
    private readonly Guid orgId = Guid.NewGuid();
    private readonly string userId = Guid.NewGuid().ToString();

    [SetUp]
    public void GrantUser()
    {
        PermissionSource.Grant(orgId, userId, PermissionKeys.DeviceView, PermissionKeys.RouterView);
    }

    private string DevicesUrl => $"/api/v1/orgs/{orgId}/devices";

    [Test]
    public async Task Requests_WhenTheCacheIsWarm_ThenOrgServiceIsNotAskedAgain()
    {
        using var client = CreateAuthenticatedClient(userId);
        (await client.GetAsync(DevicesUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        var loadsAfterFirstRequest = PermissionSource.LoadCount;

        // A different endpoint, same user and org: still served from the cache.
        (await client.GetAsync($"/api/v1/orgs/{orgId}/routers")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(DevicesUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        loadsAfterFirstRequest.Should().BeGreaterThan(0);
        PermissionSource.LoadCount.Should().Be(loadsAfterFirstRequest);
    }

    [Test]
    public async Task Requests_WhenOrgServiceGoesDownAfterTheCacheIsWarm_ThenTheAdminApiKeepsWorking()
    {
        using var client = CreateAuthenticatedClient(userId);
        (await client.GetAsync(DevicesUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        PermissionSource.Unavailable = true;
        var response = await client.GetAsync(DevicesUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Requests_WhenOrgServiceIsDownAndTheCacheIsCold_ThenTheyFailLoudlyNotWithAWrongAnswer()
    {
        PermissionSource.Unavailable = true;
        using var client = CreateAuthenticatedClient(userId);

        var response = await client.GetAsync(DevicesUrl);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
            "a member must not be told 404/403 just because their access could not be checked");
    }

    [Test]
    public async Task Requests_WhenOrgServiceComesBack_ThenTheServiceRecoversWithoutARestart()
    {
        PermissionSource.Unavailable = true;
        using var client = CreateAuthenticatedClient(userId);
        (await client.GetAsync(DevicesUrl)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        PermissionSource.Unavailable = false;
        var response = await client.GetAsync(DevicesUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Requests_WhenAFailedLoadHappened_ThenNothingWasCachedAsANonMember()
    {
        // The failed load must not poison the cache with "not a member".
        PermissionSource.Unavailable = true;
        using var client = CreateAuthenticatedClient(userId);
        await client.GetAsync(DevicesUrl);
        PermissionSource.Unavailable = false;

        var response = await client.GetAsync(DevicesUrl);

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
