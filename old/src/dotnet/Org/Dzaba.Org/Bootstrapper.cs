using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.Org;

public static class Bootstrapper
{
    public static IServiceCollection AddOrgServices(this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder, string> dbSetup,
        Func<IServiceProvider, string> connectionStringProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dbSetup);
        ArgumentNullException.ThrowIfNull(connectionStringProvider);

        services.AddTransient<IOrganizationServiceInternal, OrganizationServiceInternal>();

        services.AddDbContext<OrgDbContext>((c, o) =>
        {
            var connectionString = connectionStringProvider(c);
            dbSetup(c, o, connectionString);
        });

        services.AddTransient<Store>();

        services.AddMultiTenant<GuidTenantInfo>()
            .WithHeaderStrategy(Dzaba.Org.Contracts.Constants.OrgHeaderName)
            .WithStore<Store>(ServiceLifetime.Scoped);

        return services;
    }
}
