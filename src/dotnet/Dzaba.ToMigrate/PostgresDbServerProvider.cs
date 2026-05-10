using Microsoft.EntityFrameworkCore;

namespace Dzaba.ToMigrate;

public sealed class PostgresDbServerProvider : IDbServerProvider
{
    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        optionsBuilder.UseNpgsql(connectionString);
    }
}
