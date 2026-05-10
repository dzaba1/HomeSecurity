using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dzaba.ToMigrate;

public class JwtMockedSettings
{
    public JwtSettings Settings { get; } = new JwtSettings
    {
        Authority = "http://test",
        Audience = "home-security",
        IssuerSigningKey = "a-string-secret-at-least-256-bits-long",
        ValidateAudience = false,
        ValidateIssuer = false
    };

    public void AddMockedJwtSettings(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<JwtSettings>();
        services.AddJwtSettings(Settings);
    }

    public JwtTokenBuilder GetTokenBuilder()
    {
        return new JwtTokenBuilder()
            .WithIssuer(Settings.Authority)
            .WithAudience(Settings.Audience)
            .WithSecurityKey(Settings.IssuerSigningKey);
    }
}