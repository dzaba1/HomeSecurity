using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class OrganizationsControllerTests : OrgServiceTestFixture
{
    [Test]
    public async Task CreateAsync_WhenValidRequest_ThenOrgIsCreatedAndCreatorBecomesOwner()
    {
        var userId = Guid.NewGuid().ToString();
        var client = CreateAuthenticatedClient(userId);

        var response = await client.PostAsJsonAsync("/api/v1/orgs", new { identifier = "acme", name = "Acme" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var orgId = await ExtractIdAsync(response);

        using var db = CreateDbContext(orgId);
        (await db.Memberships.AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId)).Should().BeTrue();
        (await db.UserRoles.AnyAsync(ur => ur.TenantId == orgId && ur.UserId == userId && ur.RoleId == SystemRoles.OwnerId)).Should().BeTrue();
    }

    // No CreateAsync_WhenIdentifierAlreadyExists_ThenConflict test: EF
    // Core's InMemory provider doesn't enforce unique indexes beyond the
    // primary key (a documented InMemory limitation - Organization.Identifier
    // is a secondary unique index, not the PK), so a duplicate insert simply
    // succeeds instead of throwing. OrganizationsService.CreateAsync's catch
    // (DbUpdateException or ArgumentException) around SaveChangesAsync is
    // deliberately DB-enforced, not a pre-check SELECT, to avoid a TOCTOU
    // race under real concurrent load against Postgres - adding a pre-check
    // just to make this observable against InMemory would reintroduce that
    // race in production for the sake of a test double. Verifying this
    // specific path needs a real Postgres-backed test, not this suite.

    [Test]
    public async Task GetAsync_WhenCallerIsNotAMember_ThenNotFound()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "other-org");

        var strangerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var response = await strangerClient.GetAsync($"/api/v1/orgs/{orgId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetAsync_WhenOrgDoesNotExist_ThenNotFoundSameAsNonMember()
    {
        // Same response as GetAsync_WhenCallerIsNotAMember_ThenNotFound - a
        // real org the caller can't see must be indistinguishable from one
        // that doesn't exist, per the plan's leak-prevention design.
        var client = CreateAuthenticatedClient(Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/v1/orgs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetAsync_WhenCallerIsOwner_ThenAddMemberLinkIsPresent()
    {
        var client = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(client, "owner-links");

        var response = await client.GetAsync($"/api/v1/orgs/{orgId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("_links").TryGetProperty("addMember", out _).Should().BeTrue();
    }

    [Test]
    public async Task GetAsync_WhenCallerHasNoManagePermission_ThenAddMemberLinkIsAbsent()
    {
        var ownerClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var orgId = await CreateOrgAsync(ownerClient, "viewer-links");

        var viewerId = Guid.NewGuid().ToString();
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/members", new { userId = viewerId });
        await ownerClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/user-roles", new { userId = viewerId, roleId = SystemRoles.ViewerId });

        var viewerClient = CreateAuthenticatedClient(viewerId);
        var response = await viewerClient.GetAsync($"/api/v1/orgs/{orgId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("_links").TryGetProperty("addMember", out _).Should().BeFalse();
    }

    [Test]
    public async Task ListMineAsync_WhenCalled_ThenOnlyCallersOwnOrgsAreReturned()
    {
        var userAClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());
        var userBClient = CreateAuthenticatedClient(Guid.NewGuid().ToString());

        await CreateOrgAsync(userAClient, "org-a");
        await CreateOrgAsync(userBClient, "org-b");

        var response = await userAClient.GetAsync("/api/v1/orgs");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orgs = body.GetProperty("_embedded").GetProperty("orgs").EnumerateArray().ToList();

        orgs.Should().ContainSingle();
        orgs[0].GetProperty("identifier").GetString().Should().Be("org-a");
    }

    [Test]
    public async Task Index_WhenAuthenticated_ThenLinksToOrgsAndPermissions()
    {
        var client = CreateAuthenticatedClient(Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/v1/");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var links = body.GetProperty("_links");
        links.GetProperty("orgs").GetProperty("href").GetString().Should().Be("/api/v1/orgs");
        links.GetProperty("permissions").GetProperty("href").GetString().Should().Be("/api/v1/permissions");
    }

    [Test]
    public async Task Index_WhenNotAuthenticated_ThenUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
