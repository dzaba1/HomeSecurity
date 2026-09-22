using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class UserRolesControllerTests : OrgServiceTestFixture
{
    [Test]
    public async Task Assign_WhenCallerIsOwner_ThenAssignmentIsCreated()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "assign-org");
        var targetUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles",
            new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var db = CreateDbContext(orgId);
        (await db.UserRoles.FindAsync(targetUserId, orgId, SystemRoles.ViewerId)).Should().NotBeNull();
    }

    [Test]
    public async Task Assign_WhenAlreadyAssigned_ThenConflict()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "assign-dup-org");
        var targetUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        var payload = new { userId = targetUserId, roleId = SystemRoles.ViewerId };
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", payload);

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Unassign_WhenAssignmentExists_ThenNoContent()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "unassign-org");
        var targetUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{targetUserId}/{SystemRoles.ViewerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Unassign_WhenAssignmentDoesNotExist_ThenNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "unassign-missing-org");

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{Guid.NewGuid()}/{SystemRoles.ViewerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_WhenCallerLacksManagePermission_ThenForbidden()
    {
        // Unlike Members/Roles, every UserRoles action requires
        // org.manage_members - List is not membership-only here.
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "list-forbidden-org");
        var viewerUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = viewerUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = viewerUserId, roleId = SystemRoles.ViewerId });

        var viewerClient = CreateAuthenticatedClient(viewerUserId);
        var response = await viewerClient.GetAsync($"/api/v1/orgs/{orgId}/user-roles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
