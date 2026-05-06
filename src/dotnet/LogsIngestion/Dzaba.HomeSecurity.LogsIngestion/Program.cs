using Dzaba.HomeSecurity.Auth;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using Dzaba.HomeSecurity.Org;
using EasyNetQ;
using Finbuckle.MultiTenant.AspNetCore.Extensions;

namespace Dzaba.HomeSecurity.LogsIngestion;

public partial class Program
{
    private static IConfiguration Configuration;

    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddLogsIngestionLogging();
        builder.Services.AddRabbitMQMessageBroker(c =>
        {
            var configuration = c.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection("RabbitMQ");
            var settings = section.Get<MessageBrokerSettings>();
            return new ConnectionConfiguration
            {
                Hosts = [new HostConfiguration(settings.Host, settings.Port)],
                UserName = settings.Username,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost
            };
        });

        builder.Services.AddOrgServices(c =>
        {
            var configuration = c.GetRequiredService<IConfiguration>();
            return configuration.GetConnectionString("OrgDatabase");
        });

        builder.Services.AddAuthServices(() =>
        {
            var section = Configuration.GetSection("JwtAuth");
            return section.Get<AuthSettings>();
        });

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

        Configuration = app.Services.GetRequiredService<IConfiguration>();

        app.Run();
    }
}