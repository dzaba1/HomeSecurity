using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dzaba.ToMigrate;

public static class Bootstrapper
{
    public static IServiceCollection AddJwtSettings(this IServiceCollection services, JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(settings);
        return services;
    }

        public static IServiceCollection AddJwtAuthServices(this IServiceCollection services,
        Func<JwtSettings> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(jwtOptions =>
            {
                var options = optionsFactory();
                jwtOptions.Authority = options.Authority;
                jwtOptions.Audience = options.Audience;
                jwtOptions.RequireHttpsMetadata = options.RequireHttps;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = options.ValidateAudience,
                    ValidAudience = options.Audience,
                    ValidateIssuer = options.ValidateIssuer
                };

                if (!string.IsNullOrWhiteSpace(options.IssuerSigningKey))
                {
                    jwtOptions.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.IssuerSigningKey));
                }
            });

        return services;
    }
}