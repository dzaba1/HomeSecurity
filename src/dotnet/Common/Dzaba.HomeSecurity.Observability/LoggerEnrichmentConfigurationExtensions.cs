using Serilog;
using Serilog.Configuration;

namespace Dzaba.HomeSecurity.Observability;

public static class LoggerEnrichmentConfigurationExtensions
{
    /// <summary>Adds trace_id/span_id from the current Activity - see <see cref="ActivityEnricher"/>.</summary>
    public static LoggerConfiguration WithActivityEnricher(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);

        return enrichmentConfiguration.With<ActivityEnricher>();
    }
}
