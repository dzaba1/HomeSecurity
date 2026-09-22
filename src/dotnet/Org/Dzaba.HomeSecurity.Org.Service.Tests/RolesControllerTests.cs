using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class RolesControllerTests : OrgServiceTestFixture
{
    [Test]
    public async Task Update_WhenRoleIsSystemDefault_ThenForbidden()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "system-role-update-org");

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/orgs/{orgId}/roles/{SystemRoles.OwnerId}")
        {
            Content = JsonContent.Create(new { name = "Hacked", permissionKeys = new[] { PermissionKeys.DeviceView } }),
        };
        var response = await ownerClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Delete_WhenRoleIsSystemDefault_ThenForbidden()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "system-role-delete-org");

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/roles/{SystemRoles.OwnerId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_WhenValidRequest_ThenRoleIsCreatedWithGivenPermissions()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "custom-role-org");

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles",
            new { name = "Custom", permissionKeys = new[] { PermissionKeys.DeviceView } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("permissionKeys").EnumerateArray().Select(p => p.GetString())
            .Should().BeEquivalentTo(new[] { PermissionKeys.DeviceView });
    }

    [Test]
    public async Task Create_WhenPermissionKeyIsUnknown_ThenBadRequest()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "bad-permission-org");

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles",
            new { name = "Bogus", permissionKeys = new[] { "not.a.real.permission" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Delete_WhenCustomRole_ThenNoContentAndAssignmentsAreGone()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "custom-role-delete-org");

        var roleResp = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles",
            new { name = "Temp", permissionKeys = new[] { PermissionKeys.DeviceView } });
        var roleId = await ExtractIdAsync(roleResp);

        var assignedUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = assignedUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = assignedUserId, roleId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/roles/{roleId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = CreateDbContext(orgId);
        (await db.UserRoles.FindAsync(assignedUserId, orgId, roleId)).Should().BeNull();
    }

    [Test]
    public async Task Update_WhenCustomRolePermissionsChange_ThenAssignedUsersPermissionCacheIsInvalidated()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "custom-role-invalidate-org");

        var roleResp = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles",
            new { name = "Restricted", permissionKeys = Array.Empty<string>() });
        var roleId = await ExtractIdAsync(roleResp);

        var assignedUserId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = assignedUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = assignedUserId, roleId });

        var assignedClient = CreateAuthenticatedClient(assignedUserId);

        // Prime the permission cache with the role's original (empty) grant set.
        var before = await assignedClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members",
            new { userId = Guid.NewGuid().ToString() });
        before.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/orgs/{orgId}/roles/{roleId}")
        {
            Content = JsonContent.Create(new { name = "Restricted", permissionKeys = new[] { PermissionKeys.OrgManageMembers } }),
        };
        (await ownerClient.SendAsync(patchRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        // If invalidation didn't happen, this would still see the stale
        // (empty) cached permission set and 403 again.
        var after = await assignedClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members",
            new { userId = Guid.NewGuid().ToString() });
        after.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task List_WhenCalled_ThenIncludesBothSystemAndCustomRoles()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "list-roles-org");
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles", new { name = "Custom", permissionKeys = Array.Empty<string>() });

        var response = await ownerClient.GetAsync($"/api/v1/orgs/{orgId}/roles");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var names = body.GetProperty("_embedded").GetProperty("roles").EnumerateArray()
            .Select(r => r.GetProperty("name").GetString()).ToList();

        names.Should().BeEquivalentTo("Owner", "Admin", "Member", "Viewer", "Custom");
    }
}
