using Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;
using Dzaba.HomeSecurity.LogsIngestion.Service.Services;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using Dzaba.HomeSecurity.Observability;
using Dzaba.Utils.AspNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

const string ServiceName = "Dzaba.HomeSecurity.LogsIngestion.Service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDzabaHomeSecuritySerilog(ServiceName);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    // URI path versioning per ADR-0012 - explicit rather than the reflection-based
    // default reader, per the analyzer's own performance guidance (AV0015).
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    // Every route must carry /v{n} explicitly - matches ADR-0012's
    // rejection of an implicit-fallback version.
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
})
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Logs Ingestion API", Version = "v1" });
});

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

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(LogsPolicies.LogsWrite, policy => policy.Requirements.Add(new ScopeRequirement(LogsPolicies.LogsWrite)));
builder.Services.AddTransient<IAuthorizationHandler, ScopeAuthorizationHandler>();

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMQ");
builder.Services.AddDzabaHomeSecurityRabbitMqMessageBus(rabbitMqConnectionString);

builder.Services.AddTransient<IIngestLogsService, IngestLogsService>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(ServiceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")));

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

// Liveness: process is up, no dependency checks. Readiness: every
// registered check (RabbitMQ, registered by AddDzabaHomeSecurityRabbitMqMessageBus).
// Both unauthenticated but only ever reachable from inside the cluster -
// see docs/architecture/09-observability.md. No Postgres/Redis checks -
// this service has neither dependency.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// Gate by runtime environment/config, not Debug/Release build
// configuration - a Release build is what actually deploys to Staging too,
// and #if DEBUG would make Swagger unreachable there even when wanted.
var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Logs Ingestion API v1"));
}

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
