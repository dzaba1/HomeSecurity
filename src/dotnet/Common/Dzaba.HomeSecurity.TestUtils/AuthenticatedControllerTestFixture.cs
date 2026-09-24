using System.Net.Http.Headers;
using System.Text;
using Dzaba.AspNetUtils;
using Dzaba.TestUtils.Integration.AspNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// A controller-test fixture for a service that authenticates humans with a
/// Keycloak-issued JWT: overrides the default bearer scheme's options with a
/// symmetric test key (ControllerTestFixture's own AddMockedJwtSettings
/// helper assumes a DI-resolved JwtSettings, which the services'
/// AddJwtAuthentication(Func&lt;JwtSettings&gt;) doesn't use - PostConfigure is
/// the mechanism that actually works against that wiring), and mints
/// tokens/clients for a given Keycloak subject. A derived fixture that
/// overrides OnConfigureServices must call the base implementation.
/// </summary>
public abstract class AuthenticatedControllerTestFixture<TEntryPoint> : ControllerTestFixture<TEntryPoint>
    where TEntryPoint : class
{
    protected override void OnConfigureServices(IServiceCollection services)
    {
        base.OnConfigureServices(services);

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
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
}
