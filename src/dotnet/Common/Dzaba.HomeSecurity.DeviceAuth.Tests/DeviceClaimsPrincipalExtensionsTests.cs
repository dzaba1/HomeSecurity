using System.Security.Claims;
using FluentAssertions;

namespace Dzaba.HomeSecurity.DeviceAuth.Tests;

[TestFixture]
public class DeviceClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Test]
    public void GetTenantIdAndDeviceId_WhenClaimsArePresent_ThenReturnsThem()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var user = CreateUser(
            new Claim(DeviceClaimTypes.TenantId, tenantId.ToString()),
            new Claim(DeviceClaimTypes.DeviceId, deviceId.ToString()));

        user.GetTenantId().Should().Be(tenantId);
        user.GetDeviceId().Should().Be(deviceId);
    }

    [Test]
    public void GetTenantId_WhenTheClaimIsMissing_ThenThrows()
    {
        var act = () => CreateUser().GetTenantId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetDeviceId_WhenTheClaimIsMissing_ThenThrows()
    {
        var act = () => CreateUser().GetDeviceId();

        act.Should().Throw<InvalidOperationException>();
    }
}
