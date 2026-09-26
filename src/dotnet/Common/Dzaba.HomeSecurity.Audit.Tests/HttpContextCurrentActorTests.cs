using System.Security.Claims;
using Dzaba.HomeSecurity.Audit.Contracts;
using Dzaba.HomeSecurity.DeviceAuth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Dzaba.HomeSecurity.Audit.Tests;

[TestFixture]
public class HttpContextCurrentActorTests
{
    private static HttpContextCurrentActor CreateActor(HttpContext? httpContext)
    {
        return new HttpContextCurrentActor(new HttpContextAccessor { HttpContext = httpContext });
    }

    private static DefaultHttpContext CreateContext(params Claim[] claims)
    {
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
    }

    [Test]
    public void GetCurrent_WhenThereIsNoHttpContext_ThenTheActorIsTheSystem()
    {
        var actor = CreateActor(null).GetCurrent();

        actor.Type.Should().Be(ActorType.System);
        actor.Id.Should().Be(HttpContextCurrentActor.SystemActorId);
    }

    [Test]
    public void GetCurrent_WhenTheRequestIsNotAuthenticated_ThenTheActorIsTheSystem()
    {
        var actor = CreateActor(new DefaultHttpContext()).GetCurrent();

        actor.Type.Should().Be(ActorType.System);
    }

    [Test]
    public void GetCurrent_WhenTheTokenIsAHumansKeycloakToken_ThenTheActorIsTheUserWithTheirSubject()
    {
        var actor = CreateActor(CreateContext(new Claim("sub", "keycloak-user-1"))).GetCurrent();

        actor.Type.Should().Be(ActorType.User);
        actor.Id.Should().Be("keycloak-user-1");
    }

    [Test]
    public void GetCurrent_WhenTheTokenIsADeviceToken_ThenTheActorIsTheDeviceNotAUser()
    {
        var deviceId = Guid.NewGuid().ToString();

        var actor = CreateActor(CreateContext(
            new Claim(DeviceClaimTypes.DeviceId, deviceId),
            new Claim(DeviceClaimTypes.TenantId, Guid.NewGuid().ToString()))).GetCurrent();

        actor.Type.Should().Be(ActorType.Device);
        actor.Id.Should().Be(deviceId);
    }

    [Test]
    public void GetCurrent_WhenAnAuthenticatedPrincipalHasNoSubject_ThenItThrowsInsteadOfRecordingTheSystem()
    {
        var actor = CreateActor(CreateContext(new Claim("scope", "router:read")));

        var act = actor.GetCurrent;

        act.Should().Throw<InvalidOperationException>();
    }
}
