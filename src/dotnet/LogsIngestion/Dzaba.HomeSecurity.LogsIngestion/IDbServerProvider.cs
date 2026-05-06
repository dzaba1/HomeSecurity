using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.LogsIngestion;

public interface IDbServerProvider
{
    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}
