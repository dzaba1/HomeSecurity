using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;

namespace Dzaba.HomeSecurity.Observability;

public static class Bootstrapper
{
    private static readonly Uri DefaultOtlpEndpoint = new("http://localhost:4317");

    /// <summary>
    /// Console-only, structured JSON Serilog pipeline shared by every
    /// service: FromLogContext + thread id + Activity trace/span id
    /// enrichment, plus fixed service_name/environment properties. Shipping
    /// (e.g. to Loki) is a cluster-infra concern, not something a service
    /// configures for itself. Never destructure request/command objects -
    /// device secrets/JWTs/credentials must never be logged, per
    /// docs/architecture/09-observability.md.
    /// </summary>
    public static IHostBuilder UseDzabaHomeSecuritySerilog(this IHostBuilder hostBuilder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        return hostBuilder.UseSerilog((context, services, configuration) => configuration
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithActivityEnricher()
            .Enrich.WithProperty("service_name", serviceName)
            .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console(new JsonFormatter()));
    }

    /// <summary>
    /// Metrics (ASP.NET Core + HttpClient + runtime) and tracing (ASP.NET
    /// Core + HttpClient) shared by every service, both exported via OTLP to
    /// the cluster's OpenTelemetry Collector - see
    /// docs/architecture/09-observability.md. <paramref name="endpointFactory"/>
    /// defaults to the local collector's default gRPC endpoint; pass one to
    /// override how a service resolves it (e.g. from configuration).
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        Func<Uri>? endpointFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var otlpEndpoint = endpointFactory?.Invoke() ?? DefaultOtlpEndpoint;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = otlpEndpoint))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = otlpEndpoint));

        return services;
    }
}
