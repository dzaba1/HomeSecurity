using Dzaba.BasicAuthentication;
using Dzaba.HomeSecurity.LogsIngestion;
using Dzaba.HomeSecurity.LogsIngestion.Auth;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using EasyNetQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogsIngestionLogging();
builder.Services.AddRabbitMQMessageBroker(c =>
{
    var section = builder.Configuration.GetSection("RabbitMQ");
    var settings = section.Get<MessageBrokerSettings>();
    return new ConnectionConfiguration
    {
        Hosts = [new HostConfiguration(settings.Host, settings.Port)],
        UserName = settings.Username,
        Password = settings.Password,
        VirtualHost = settings.VirtualHost
    };
});

builder.Services.AddTransient<IPasswordHasher, IPasswordHasher>();

builder.Services.AddBasicAuthentication<BasicAuthHandler>();
builder.Services.AddAuthentication(o =>
{
    o.AddBasicAuthenticationScheme(true);
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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
