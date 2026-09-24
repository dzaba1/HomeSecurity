using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Org.Service.Data;

public static class Bootstrapper
{
    public static IServiceCollection AddDzabaHomeSecurityDataServices(this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder, string> dbSetup,
        Func<IServiceProvider, string> connectionStringProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dbSetup);
        ArgumentNullException.ThrowIfNull(connectionStringProvider);

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = connectionStringProvider(sp);
            dbSetup(sp, options, connectionString);
        });

        return services;
    }
}
