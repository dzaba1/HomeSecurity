using System.Security.Claims;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Dzaba.HomeSecurity.Authorization.Tests;

[TestFixture]
public class TenantResolutionMiddlewareTests
{
    private Mock<ITenantMembershipChecker> membership = null!;
    private MutableTenantContext tenantContext = null!;
    private bool nextCalled;
    private TenantResolutionMiddleware middleware = null!;

    [SetUp]
    public void SetUp()
    {
        membership = new Mock<ITenantMembershipChecker>();
        tenantContext = new MutableTenantContext();
        nextCalled = false;
        middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
    }

    private static DefaultHttpContext CreateContext(object? orgId, string? userId)
    {
        var context = new DefaultHttpContext();
        if (orgId is not null)
        {
            context.Request.RouteValues["orgId"] = orgId;
        }

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId)], "test"));
        }

        return context;
    }

    [Test]
    public async Task Invoke_WhenTheRouteHasNoOrgId_ThenContinuesWithAnEmptyTenant()
    {
        var context = CreateContext(null, "user-1");

        await middleware.InvokeAsync(context, tenantContext, membership.Object);

        nextCalled.Should().BeTrue();
        tenantContext.TenantId.Should().Be(Guid.Empty);
        membership.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Invoke_WhenTheCallerIsAMember_ThenTheTenantIsSetAndTheRequestContinues()
    {
        var orgId = Guid.NewGuid();
        membership.Setup(m => m.IsMemberAsync(orgId, "user-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = CreateContext(orgId.ToString(), "user-1");

        await middleware.InvokeAsync(context, tenantContext, membership.Object);

        nextCalled.Should().BeTrue();
        tenantContext.TenantId.Should().Be(orgId);
    }

    [Test]
    public async Task Invoke_WhenTheCallerIsNotAMember_ThenNotFoundNotForbiddenAndTheRequestStops()
    {
        var orgId = Guid.NewGuid();
        membership.Setup(m => m.IsMemberAsync(orgId, "user-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var context = CreateContext(orgId.ToString(), "user-1");

        await middleware.InvokeAsync(context, tenantContext, membership.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextCalled.Should().BeFalse();
    }

    [Test]
    public async Task Invoke_WhenTheCallerIsNotAuthenticated_ThenUnauthorizedWithoutCheckingMembership()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), null);

        await middleware.InvokeAsync(context, tenantContext, membership.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        membership.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Invoke_WhenTheOrgIdIsNotAGuid_ThenBadRequest()
    {
        var context = CreateContext("not-a-guid", "user-1");

        await middleware.InvokeAsync(context, tenantContext, membership.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        nextCalled.Should().BeFalse();
        membership.VerifyNoOtherCalls();
    }
}
