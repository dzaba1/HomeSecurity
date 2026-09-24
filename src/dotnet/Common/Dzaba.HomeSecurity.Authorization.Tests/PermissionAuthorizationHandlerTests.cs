using System.Security.Claims;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace Dzaba.HomeSecurity.Authorization.Tests;

[TestFixture]
public class PermissionAuthorizationHandlerTests
{
    private const string PermissionKey = "router.manage";
    private static readonly Guid TenantId = Guid.NewGuid();

    private Mock<IPermissionEvaluator> evaluator = null!;
    private PermissionAuthorizationHandler handler = null!;

    [SetUp]
    public void SetUp()
    {
        var tenantContext = new MutableTenantContext();
        tenantContext.SetTenantId(TenantId);
        evaluator = new Mock<IPermissionEvaluator>();
        handler = new PermissionAuthorizationHandler(tenantContext, evaluator.Object);
    }

    private static AuthorizationHandlerContext CreateContext(params Claim[] claims) =>
        new([new PermissionRequirement(PermissionKey)], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);

    [Test]
    public async Task Handle_WhenTheEvaluatorGrantsThePermission_ThenSucceeds()
    {
        evaluator.Setup(e => e.HasPermissionAsync("user-1", TenantId, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = CreateContext(new Claim("sub", "user-1"));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Test]
    public async Task Handle_WhenTheEvaluatorDeniesThePermission_ThenDoesNotSucceed()
    {
        evaluator.Setup(e => e.HasPermissionAsync("user-1", TenantId, PermissionKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var context = CreateContext(new Claim("sub", "user-1"));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Test]
    public async Task Handle_WhenThePrincipalHasNoUserId_ThenDoesNotSucceedAndNeverAsksTheEvaluator()
    {
        var context = CreateContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        evaluator.Verify(e => e.HasPermissionAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenAskingTheEvaluator_ThenUsesTheResolvedTenantNotAClientSuppliedOne()
    {
        evaluator.Setup(e => e.HasPermissionAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = CreateContext(new Claim("sub", "user-1"), new Claim("tenant_id", Guid.NewGuid().ToString()));

        await handler.HandleAsync(context);

        evaluator.Verify(e => e.HasPermissionAsync("user-1", TenantId, PermissionKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}
