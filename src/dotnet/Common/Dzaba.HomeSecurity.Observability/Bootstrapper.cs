using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Json;

namespace Dzaba.HomeSecurity.Observability;

public static class Bootstrapper
{
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
}
