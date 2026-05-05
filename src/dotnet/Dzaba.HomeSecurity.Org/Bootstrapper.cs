using Dzaba.HomeSecurity.Org.Contracts;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Org;

public static class Bootstrapper
{
    public static IServiceCollection AddOrgServices(this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStringProvider);

        services.AddTransient<IOrgService, OrgService>();

        services.AddDbContext<OrgDbContext>((c, o) => o.UseNpgsql(connectionStringProvider(c)));

        services.AddMultiTenant<OrgTenantInfo>()
            .WithHeaderStrategy(Constants.OrgHeaderName)
            .WithEFCoreStore<OrgDbContext, OrgTenantInfo>();

        return services;
    }
}
