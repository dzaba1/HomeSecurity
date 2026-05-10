using Microsoft.Extensions.DependencyInjection;

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
}