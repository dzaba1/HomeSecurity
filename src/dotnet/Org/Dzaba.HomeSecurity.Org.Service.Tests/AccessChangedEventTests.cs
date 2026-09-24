using System.Net;
using System.Net.Http.Json;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Contracts;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// Org.Service publishes a coarse access.changed notification whenever a
/// user's membership or role assignments in a tenant change - see
/// access_changed_message.json. Nothing consumes it yet, so the only thing
/// guarding it is these tests.
/// </summary>
[TestFixture]
public class AccessChangedEventTests : OrgServiceTestFixture
{
    private HttpClient ownerClient = null!;
    private Guid orgId;
    private string targetUserId = null!;

    [SetUp]
    public async Task CreateOrgWithOwner()
    {
        ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        orgId = await CreateOrgAsync(ownerClient, $"access-changed-{Guid.NewGuid():N}");
        targetUserId = Guid.NewGuid().ToString();
    }

    [TearDown]
    public void DisposeOwnerClient() => ownerClient.Dispose();

    private AccessChangedMessage[] AccessChangedFor(string userId) =>
        [.. MessageBus.Published
            .Where(p => p.RoutingKey == "access.changed")
            .Select(p => p.Message)
            .OfType<AccessChangedMessage>()
            .Where(m => m.TenantId == orgId && m.UserId == userId)];

    [Test]
    public async Task AddMember_WhenSuccessful_ThenPublishesAccessChangedForThatUser()
    {
        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = AccessChangedFor(targetUserId).Should().ContainSingle().Subject;
        message.ChangedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task RemoveMember_WhenSuccessful_ThenPublishesAccessChangedForThatUser()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{targetUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AccessChangedFor(targetUserId).Should().HaveCount(2, "one for the add, one for the removal");
    }

    [Test]
    public async Task AssignRole_WhenSuccessful_ThenPublishesAccessChangedForThatUser()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });

        var response = await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles",
            new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AccessChangedFor(targetUserId).Should().HaveCount(2, "one for the add, one for the assignment");
    }

    [Test]
    public async Task RevokeRole_WhenSuccessful_ThenPublishesAccessChangedForThatUser()
    {
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = targetUserId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = targetUserId, roleId = SystemRoles.ViewerId });

        var response = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{targetUserId}/{SystemRoles.ViewerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AccessChangedFor(targetUserId).Should().HaveCount(3);
    }

    [Test]
    public async Task Mutations_WhenTheyFail_ThenNothingIsPublished()
    {
        var missingMember = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/members/{targetUserId}");
        var missingAssignment = await ownerClient.DeleteAsync($"/api/v1/orgs/{orgId}/user-roles/{targetUserId}/{SystemRoles.ViewerId}");

        missingMember.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingAssignment.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AccessChangedFor(targetUserId).Should().BeEmpty();
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
        AccessChangedFor(targetUserId).Should().BeEmpty();
    }
}
