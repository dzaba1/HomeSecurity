using Microsoft.EntityFrameworkCore;

namespace Dzaba.ToMigrate;

public interface IDbServerProvider
{
    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}
