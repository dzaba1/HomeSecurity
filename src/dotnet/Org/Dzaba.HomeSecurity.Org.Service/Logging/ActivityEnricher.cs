using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Dzaba.HomeSecurity.Org.Service.Logging;

/// <summary>
/// Adds trace_id/span_id from the current Activity so a log line can be
/// pivoted to its full OpenTelemetry trace and back - see
/// docs/architecture/09-observability.md.
/// </summary>
internal sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace_id", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span_id", activity.SpanId.ToString()));
    }
}
