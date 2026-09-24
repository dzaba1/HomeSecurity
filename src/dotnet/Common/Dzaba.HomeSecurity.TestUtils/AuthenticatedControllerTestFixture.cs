using System.Net.Http.Headers;
using System.Text;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.DeviceAuth;
using Dzaba.TestUtils.Integration.AspNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// A controller-test fixture for a service that authenticates callers with
/// JWTs: overrides the bearer options of the default scheme - and of
/// <see cref="DeviceTokenScheme"/>, when a service registers its device-token
/// validation under a separate name - with a symmetric test key
/// (ControllerTestFixture's own AddMockedJwtSettings helper assumes a
/// DI-resolved JwtSettings, which the services'
/// AddJwtAuthentication(Func&lt;JwtSettings&gt;) doesn't use - PostConfigure is
/// the mechanism that actually works against that wiring), and mints
/// tokens/clients for a Keycloak subject or for a device. A derived fixture
/// that overrides OnConfigureServices must call the base implementation.
/// </summary>
public abstract class AuthenticatedControllerTestFixture<TEntryPoint> : ControllerTestFixture<TEntryPoint>
    where TEntryPoint : class
{
    /// <summary>
    /// The authentication scheme the service under test validates device
    /// tokens with; null when it does so under the default scheme (as a
    /// service that only authenticates devices does).
    /// </summary>
    protected virtual string? DeviceTokenScheme => null;

    protected override void OnConfigureServices(IServiceCollection services)
    {
        base.OnConfigureServices(services);

        ConfigureTestSigningKey(services, JwtBearerDefaults.AuthenticationScheme);
        if (DeviceTokenScheme is not null)
        {
            ConfigureTestSigningKey(services, DeviceTokenScheme);
        }
    }

    private void ConfigureTestSigningKey(IServiceCollection services, string scheme)
    {
        services.PostConfigure<JwtBearerOptions>(scheme, jwtOptions =>
        {
            jwtOptions.Authority = null;
            jwtOptions.RequireHttpsMetadata = false;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSettings.Settings.IssuerSigningKey)),
            };
        });
    }

    protected string CreateToken(string userId) =>
        JwtSettings.GetTokenBuilder().WithSubject(userId).Build().EncodeToString();

    protected HttpClient CreateAuthenticatedClient(string userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    protected string CreateDeviceToken(Guid tenantId, Guid deviceId, string scope) =>
        JwtSettings.GetTokenBuilder()
            .WithClaim(DeviceClaimTypes.TenantId, tenantId.ToString())
            .WithClaim(DeviceClaimTypes.DeviceId, deviceId.ToString())
            .WithClaim(DeviceClaimTypes.Scope, scope)
            .Build()
            .EncodeToString();

    protected HttpClient CreateDeviceClient(Guid tenantId, Guid deviceId, string scope)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDeviceToken(tenantId, deviceId, scope));
        return client;
    }
}
