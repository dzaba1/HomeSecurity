using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
}
