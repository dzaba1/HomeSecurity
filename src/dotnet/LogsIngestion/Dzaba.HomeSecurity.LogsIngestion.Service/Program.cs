using Dzaba.HomeSecurity.DeviceAuth;
using Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;
using Dzaba.HomeSecurity.LogsIngestion.Service.Services;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using Dzaba.HomeSecurity.Observability;
using Dzaba.HomeSecurity.WebApi;
using Dzaba.Utils.AspNet;
using Serilog;

const string ServiceName = "Dzaba.HomeSecurity.LogsIngestion.Service";
var apiVersion = new Version(1, 0);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDzabaHomeSecuritySerilog(ServiceName);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddDzabaHomeSecurityApiVersioning(apiVersion);
builder.Services.AddDzabaHomeSecuritySwaggerGen("Logs Ingestion API", apiVersion);

// Device tokens have no OIDC discovery endpoint to validate against (unlike
// the Keycloak-issued human tokens Org validates) - they're signed by a
// small, purpose-built token endpoint (docs/architecture/03-security-and-identity.md)
// using a symmetric key this service and that endpoint both hold. No
// ValidIssuer wiring exists in AddJwtAuthentication's static-key path, so
// issuer validation is explicitly off here - a deliberate deviation from
// Org's Keycloak-authority setup, not an oversight.
builder.Services.AddJwtAuthentication(() => new JwtSettings
{
    Audience = builder.Configuration["Authentication:DeviceTokens:Audience"],
    IssuerSigningKey = builder.Configuration["Authentication:DeviceTokens:SigningKey"],
    RequireHttps = builder.Environment.IsProduction(),
    ValidateAudience = true,
    ValidateIssuer = false,
});

builder.Services.AddDzabaHomeSecurityDeviceScopePolicy(LogsPolicies.LogsWrite);

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMQ");
builder.Services.AddDzabaHomeSecurityRabbitMqMessageBus(rabbitMqConnectionString);

builder.Services.AddTransient<IIngestLogsService, IngestLogsService>();

builder.Services.AddDzabaHomeSecurityOpenTelemetry(ServiceName,
    () => new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317"));

var app = builder.Build();

app.UseSerilogRequestLogging();

// Defaults to true (secure by default per docs/README.md's HTTPS-only
// principle). Overridable via config/env var so environments that
// terminate TLS in front of this service - e.g. the local docker-compose
// stack, where nothing yet sits in front of it - can disable the redirect
// instead of 307-looping to a TLS port the container doesn't serve.
if (builder.Configuration.GetValue("Security:UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// RabbitMQ's check is registered by AddDzabaHomeSecurityRabbitMqMessageBus
// above - see MapDzabaHomeSecurityHealthChecks for the endpoint shape.
app.MapDzabaHomeSecurityHealthChecks();

app.UseDzabaHomeSecuritySwaggerUI("Logs Ingestion API", apiVersion);

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
