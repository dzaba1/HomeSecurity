using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.LogsIngestion.Service.Messages;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Services;

internal sealed class IngestLogsService : IIngestLogsService
{
    private readonly ILogger<IngestLogsService> logger;
    private readonly IMessageBus messageBus;

    public IngestLogsService(ILogger<IngestLogsService> logger, IMessageBus messageBus)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(messageBus);

        this.logger = logger;
        this.messageBus = messageBus;
    }

    public Task IngestAsync(Guid tenantId, Guid deviceId, IngestLogsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new LogBatchMessage
        {
            MessageId = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            Devices = [.. request.Devices],
        };

        logger.LogInformation("Ingesting log batch {MessageId} for tenant {TenantId}, device {DeviceId}, {DeviceCount} device(s)",
            message.MessageId, tenantId, deviceId, message.Devices.Count);

        return messageBus.PublishAsync(RoutingKeys.LogsIngested, message, cancellationToken);
    }
}
