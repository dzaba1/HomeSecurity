using Dzaba.ToMigrate;
using Finbuckle.MultiTenant.AspNetCore.Extensions;

namespace Dzaba.Org.Service;

public partial class Program
{
    private static IServiceProvider Container;

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOrgServices((c, d, cs) =>
        {
            var dbProvider = c.GetRequiredService<IDbServerProvider>();
            dbProvider.Configure(d, cs);
        },
        c =>
        {
            var configuration = c.GetRequiredService<IConfiguration>();
            return configuration.GetConnectionString("OrgDatabase");
        });

        builder.Services.AddJwtAuthServices(() => Container.GetRequiredService<JwtSettings>());

        builder.Services.AddTransient<IDbServerProvider, PostgresDbServerProvider>();

        builder.Services.AddAuthorization();

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMultiTenant();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        Container = app.Services;

        app.Run();
    }
}