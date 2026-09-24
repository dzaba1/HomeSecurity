using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace Dzaba.HomeSecurity.DeviceAuth.Tests;

[TestFixture]
public class ScopeAuthorizationHandlerTests
{
    private static async Task<bool> EvaluateAsync(string requiredScope, params Claim[] claims)
    {
        var requirement = new ScopeRequirement(requiredScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new ScopeAuthorizationHandler().HandleAsync(context);

        return context.HasSucceeded;
    }

    [Test]
    public async Task Handle_WhenTheScopeClaimContainsTheRequiredScope_ThenSucceeds()
    {
        (await EvaluateAsync("router:read", new Claim(DeviceClaimTypes.Scope, "logs:write router:read"))).Should().BeTrue();
    }

    [Test]
    public async Task Handle_WhenTheScopeClaimOnlyContainsAnotherScope_ThenDoesNotSucceed()
    {
        (await EvaluateAsync("router:read", new Claim(DeviceClaimTypes.Scope, "logs:write"))).Should().BeFalse();
    }

    [Test]
    public async Task Handle_WhenTheRequiredScopeIsOnlyAPrefixOfAGrantedScope_ThenDoesNotSucceed()
    {
        (await EvaluateAsync("router:read", new Claim(DeviceClaimTypes.Scope, "router:readwrite"))).Should().BeFalse();
    }

    [Test]
    public async Task Handle_WhenThereIsNoScopeClaim_ThenDoesNotSucceed()
    {
        (await EvaluateAsync("router:read")).Should().BeFalse();
    }
}
