using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Contracts;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class AccessContextControllerTests : OrgServiceTestFixture
{
    [Test]
    public async Task Get_WhenCallerIsTheOrgOwner_ThenReturnsTheirEffectivePermissionKeys()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "access-context-owner-org");

        var response = await ownerClient.GetAsync($"/api/v1/orgs/{orgId}/access-context");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccessContextResponse>();
        body!.PermissionKeys.Should().BeEquivalentTo(PermissionKeys.All);
    }

    [Test]
    public async Task Get_WhenCallerIsAMemberWithNoRoles_ThenReturnsAnEmptyPermissionSet()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "access-context-norole-org");
        var memberUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = memberUserId });

        var response = await CreateAuthenticatedClient(memberUserId).GetAsync($"/api/v1/orgs/{orgId}/access-context");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AccessContextResponse>();
        body!.PermissionKeys.Should().BeEmpty();
    }

    [Test]
    public async Task Get_WhenCallerIsNotAMember_ThenNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "access-context-stranger-org");

        var response = await CreateAuthenticatedClient(Guid.NewGuid().ToString())
            .GetAsync($"/api/v1/orgs/{orgId}/access-context");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
