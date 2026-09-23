using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.DbServer;

internal sealed class NpgsqlDbServerProvider : IDbServerProvider
{
    private readonly string migrationsAssemblyName;

    public NpgsqlDbServerProvider(string migrationsAssemblyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(migrationsAssemblyName);

        this.migrationsAssemblyName = migrationsAssemblyName;
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        // Migrations live in the consuming service's assembly, not in the
        // (provider-agnostic) project a DbContext itself is defined in.
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(migrationsAssemblyName));
    }
}
