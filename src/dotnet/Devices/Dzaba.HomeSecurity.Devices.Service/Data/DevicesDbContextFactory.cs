using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dzaba.HomeSecurity.Devices.Service.Data;

/// <summary>
/// Lets `dotnet ef migrations add` construct a DevicesDbContext without a
/// running app/DI container. The tenant value here is never used for
/// anything - migrations don't run queries through the model's query
/// filters.
/// </summary>
internal sealed class DevicesDbContextFactory : IDesignTimeDbContextFactory<DevicesDbContext>
{
    public DevicesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DevicesDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=homesecurity_devices;Username=homesecurity;Password=design-time-only",
            b => b.MigrationsAssembly(typeof(DevicesDbContextFactory).Assembly.GetName().Name));

        return new DevicesDbContext(optionsBuilder.Options, new StaticTenantContext(Guid.Empty));
    }
}
