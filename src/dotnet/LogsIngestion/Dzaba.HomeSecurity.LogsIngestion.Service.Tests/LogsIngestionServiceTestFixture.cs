using System.Net.Http.Headers;
using System.Text;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.HomeSecurity.TestUtils;
using Dzaba.TestUtils.Integration.AspNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Tests;

/// <summary>
/// Base fixture for every controller test: swaps the real RabbitMQ-backed
/// IMessageBus for FakeMessageBus (this service has no database, so unlike
/// Org.Service.Tests there's no provider seam to swap), and overrides JWT
/// bearer options with a symmetric test key - same reasoning as Org's own
/// fixture: AddJwtAuthentication(Func&lt;JwtSettings&gt;) isn't DI-resolved, so
/// PostConfigure is what actually reaches it.
/// </summary>
public abstract class LogsIngestionServiceTestFixture : ControllerTestFixture<Program>
{
    protected FakeMessageBus MessageBus { get; private set; } = null!;

    protected override void OnConfigureServices(IServiceCollection services)
    {
        MessageBus = new FakeMessageBus();
        services.RemoveAll<IMessageBus>();
        services.AddSingleton<IMessageBus>(MessageBus);

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

    protected string CreateToken(Guid tenantId, Guid deviceId, string scope = LogsPolicies.LogsWrite) =>
        JwtSettings.GetTokenBuilder()
            .WithClaim(DeviceClaimTypes.TenantId, tenantId.ToString())
            .WithClaim(DeviceClaimTypes.DeviceId, deviceId.ToString())
            .WithClaim(DeviceClaimTypes.Scope, scope)
            .Build()
            .EncodeToString();

    protected HttpClient CreateDeviceClient(Guid tenantId, Guid deviceId, string scope = LogsPolicies.LogsWrite)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(tenantId, deviceId, scope));
        return client;
    }
}
