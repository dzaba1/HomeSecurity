using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.DbServer;

public static class Bootstrapper
{
    /// <summary>
    /// Registers the Npgsql-backed <see cref="IDbServerProvider"/>.
    /// <paramref name="migrationsAssembly"/> is the assembly the calling
    /// service's EF Core migrations live in (typically its own).
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityNpgsqlDbServer(this IServiceCollection services, Assembly migrationsAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(migrationsAssembly);

        var migrationsAssemblyName = migrationsAssembly.GetName().Name
            ?? throw new ArgumentException("The migrations assembly has no name.", nameof(migrationsAssembly));

        services.AddTransient<IDbServerProvider>(_ => new NpgsqlDbServerProvider(migrationsAssemblyName));

        return services;
    }
}
