using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

internal sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions options) : base(options)
    {
    }
}
