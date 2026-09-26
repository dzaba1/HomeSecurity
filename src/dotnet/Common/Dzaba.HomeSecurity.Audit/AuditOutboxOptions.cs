namespace Dzaba.HomeSecurity.Audit;

public sealed class AuditOutboxOptions
{
    public const string SectionName = "AuditOutbox";

    /// <summary>How long the relay sleeps when the outbox is empty or a publish failed.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>The most rows the relay publishes per pass over the outbox.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// The health check reports Degraded once the oldest unpublished event is
    /// older than this: an audit pipeline that has silently stalled is worse
    /// than none.
    /// </summary>
    public TimeSpan MaxBacklogAge { get; set; } = TimeSpan.FromMinutes(5);
}
