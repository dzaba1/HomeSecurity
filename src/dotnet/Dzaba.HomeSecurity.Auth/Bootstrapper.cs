using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Auth;

public static class Bootstrapper
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
