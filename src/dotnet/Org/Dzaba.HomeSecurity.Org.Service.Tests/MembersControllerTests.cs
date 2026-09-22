using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class MembersControllerTests : OrgServiceTestFixture
{
    [Test]
    public async Task Add_WhenCallerIsOwner_ThenMemberIsAdded()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "members-owner-org");

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members",
            new { userId = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Add_WhenCallerHoldsOnlyMemberRole_ThenForbidden()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "members-forbidden-org");

        var memberUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = memberUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = memberUserId, roleId = SystemRoles.MemberId });

        var memberClient = CreateAuthenticatedClient(memberUserId);
        var response = await memberClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members",
            new { userId = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Add_WhenCallerIsAuthenticatedButNotAMember_ThenNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "members-nonmember-org");

        var strangerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var response = await strangerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members",
            new { userId = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Remove_WhenMemberExists_ThenNoContentAndMembershipDeleted()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "remove-org");
        var memberUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = memberUserId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{memberUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = CreateDbContext(orgId);
        (await db.Memberships.FindAsync(orgId, memberUserId)).Should().BeNull();
    }

    [Test]
    public async Task Remove_WhenMemberDoesNotExist_ThenNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "remove-missing-org");

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Remove_WhenMemberHadARole_ThenRoleAssignmentIsAlsoRemoved()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "remove-role-org");
        var memberUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = memberUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = memberUserId, roleId = SystemRoles.ViewerId });

        await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{memberUserId}");

        using var db = CreateDbContext(orgId);
        (await db.UserRoles.FindAsync(memberUserId, orgId, SystemRoles.ViewerId)).Should().BeNull();
    }

    [Test]
    public async Task List_WhenCallerIsMemberWithoutManagePermission_ThenAddLinkIsAbsentButListSucceeds()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "members-list-org");
        var viewerId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = viewerId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = viewerId, roleId = SystemRoles.ViewerId });

        var viewerClient = CreateAuthenticatedClient(viewerId);
        var response = await viewerClient.GetAsync($"/api/v1/orgs/{orgId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("_links").TryGetProperty("add", out _).Should().BeFalse();
    }
}
