using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// Every operation Org.Service performs on organizations, memberships, role
/// assignments and roles publishes one domain event named for what happened,
/// with its extended data - see domain_event_message.json. Audit.Service (and
/// any other future subscriber) relies on these names, actors, targets and
/// metadata, and nothing else guards them.
/// </summary>
[TestFixture]
public class OrgDomainEventTests : OrgServiceTestFixture
{
    private string ownerId = null!;
    private HttpClient ownerClient = null!;
    private Guid orgId;
    private string targetUserId = null!;

    [SetUp]
    public async Task CreateOrgWithOwner()
    {
        ownerId = Guid.NewGuid().ToString();
        ownerClient = CreateAuthenticatedClient(ownerId);
        orgId = await CreateOrgAsync(ownerClient, $"events-{Guid.NewGuid():N}");
        targetUserId = Guid.NewGuid().ToString();
    }

    [TearDown]
    public void DisposeOwnerClient() => ownerClient.Dispose();

    private DomainEventMessage[] Published(string eventName) =>
        [.. MessageBus.Published
            .Where(p => p.RoutingKey == eventName)
            .Select(p => p.Message)
            .OfType<DomainEventMessage>()
            .Where(m => m.TenantId == orgId)];

    private static IReadOnlyDictionary<string, object?> Metadata(DomainEventMessage message) =>
        message.Metadata.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;

    private async Task<Guid> CreateCustomRoleAsync(string name, params string[] permissionKeys)
    {
        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/roles", new { name, permissionKeys });
        response.EnsureSuccessStatusCode();
        return await ExtractIdAsync(response);
    }

    [Test]
    public async Task CreateOrganization_WhenSuccessful_ThenPublishesOrganizationCreatedByTheCallerForTheNewTenant()
    {
        var message = Published(DomainEventNames.OrganizationCreated).Should().ContainSingle().Subject;

        message.Actor.Type.Should().Be(ActorType.User);
        message.Actor.Id.Should().Be(ownerId);
        message.Target.Type.Should().Be(DomainEventTargetTypes.Organization);
        message.Target.Id.Should().Be(orgId.ToString());
        message.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        Metadata(message)["name"].Should().NotBeNull();
        Metadata(message).Should().ContainKey("identifier");
    }

    [Test]
    public async Task AddMember_WhenSuccessful_ThenPublishesMembershipAddedNamingTheCallerAndTheMember()
    {
        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = Published(DomainEventNames.MembershipAdded).Should().ContainSingle().Subject;
        message.Actor.Id.Should().Be(ownerId);
        message.Target.Type.Should().Be(DomainEventTargetTypes.Membership);
        message.Target.Id.Should().Be(targetUserId);
    }

    [Test]
    public async Task RemoveMember_WhenTheyHoldARole_ThenPublishesMembershipRemovedListingTheRolesTheyLost()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{targetUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var message = Published(DomainEventNames.MembershipRemoved).Should().ContainSingle().Subject;
        message.Actor.Id.Should().Be(ownerId);
        message.Target.Id.Should().Be(targetUserId);
        Metadata(message)["removedRoleIds"].Should().BeEquivalentTo(new[] { SystemRoles.ViewerId });
    }

    [Test]
    public async Task AssignRole_WhenSuccessful_ThenPublishesRoleAssignedWithTheRoleName()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles",
            new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = Published(DomainEventNames.RoleAssigned).Should().ContainSingle().Subject;
        message.Actor.Id.Should().Be(ownerId);
        message.Target.Type.Should().Be(DomainEventTargetTypes.UserRole);
        message.Target.Id.Should().Be($"{targetUserId}:{SystemRoles.ViewerId}");
        Metadata(message)["userId"].Should().Be(targetUserId);
        Metadata(message)["roleId"].Should().Be(SystemRoles.ViewerId);
        Metadata(message)["roleName"].Should().Be(SystemRoles.ViewerName);
    }

    [Test]
    public async Task RevokeRole_WhenSuccessful_ThenPublishesRoleUnassignedWithTheRoleName()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{targetUserId}/{SystemRoles.ViewerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var message = Published(DomainEventNames.RoleUnassigned).Should().ContainSingle().Subject;
        message.Target.Id.Should().Be($"{targetUserId}:{SystemRoles.ViewerId}");
        Metadata(message)["roleName"].Should().Be(SystemRoles.ViewerName);
    }

    [Test]
    public async Task CreateRole_WhenSuccessful_ThenPublishesRoleCreatedWithNameAndPermissionKeys()
    {
        var roleId = await CreateCustomRoleAsync("Auditors", PermissionKeys.AuditView, PermissionKeys.LogsView);

        var message = Published(DomainEventNames.RoleCreated).Should().ContainSingle().Subject;
        message.Actor.Id.Should().Be(ownerId);
        message.Target.Type.Should().Be(DomainEventTargetTypes.Role);
        message.Target.Id.Should().Be(roleId.ToString());
        Metadata(message)["name"].Should().Be("Auditors");
        Metadata(message)["permissionKeys"].Should().BeEquivalentTo(new[] { PermissionKeys.AuditView, PermissionKeys.LogsView });
    }

    [Test]
    public async Task UpdateRole_WhenNameAndPermissionsChange_ThenPublishesRoleUpdatedWithTheDiff()
    {
        var roleId = await CreateCustomRoleAsync("Auditors", PermissionKeys.AuditView, PermissionKeys.LogsView);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/orgs/{orgId}/roles/{roleId}")
        {
            Content = JsonContent.Create(new { name = "Log readers", permissionKeys = new[] { PermissionKeys.LogsView, PermissionKeys.DeviceView } }),
        };
        var response = await ownerClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var message = Published(DomainEventNames.RoleUpdated).Should().ContainSingle().Subject;
        message.Target.Id.Should().Be(roleId.ToString());
        Metadata(message)["previousName"].Should().Be("Auditors");
        Metadata(message)["name"].Should().Be("Log readers");
        Metadata(message)["addedPermissionKeys"].Should().BeEquivalentTo(new[] { PermissionKeys.DeviceView });
        Metadata(message)["removedPermissionKeys"].Should().BeEquivalentTo(new[] { PermissionKeys.AuditView });
    }

    [Test]
    public async Task DeleteRole_WhenAssigned_ThenPublishesRoleDeletedWithHowManyAssignmentsWentWithIt()
    {
        var roleId = await CreateCustomRoleAsync("Temp", PermissionKeys.LogsView);
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = targetUserId, roleId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/roles/{roleId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var message = Published(DomainEventNames.RoleDeleted).Should().ContainSingle().Subject;
        message.Target.Id.Should().Be(roleId.ToString());
        Metadata(message)["name"].Should().Be("Temp");
        Metadata(message)["unassignedAssignments"].Should().Be(1);
    }

    [Test]
    public async Task Mutations_WhenTheyFail_ThenNothingIsPublished()
    {
        var missingMember = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{targetUserId}");
        var missingAssignment = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{targetUserId}/{SystemRoles.ViewerId}");

        missingMember.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingAssignment.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Published(DomainEventNames.MembershipRemoved).Should().BeEmpty();
        Published(DomainEventNames.RoleUnassigned).Should().BeEmpty();
    }

    [Test]
    public async Task AddMember_WhenTheCallerLacksThePermission_ThenNothingIsPublished()
    {
        var viewerId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = viewerId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = viewerId, roleId = SystemRoles.ViewerId });

        var response = await CreateAuthenticatedClient(viewerId)
            .PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Published(DomainEventNames.MembershipAdded).Should().NotContain(m => m.Target.Id == targetUserId);
    }
}
