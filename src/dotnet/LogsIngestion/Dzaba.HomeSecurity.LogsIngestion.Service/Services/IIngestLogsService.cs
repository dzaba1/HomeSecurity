using Dzaba.HomeSecurity.LogsIngestion.Contracts;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Services;

public interface IIngestLogsService
{
    Task IngestAsync(Guid tenantId, Guid deviceId, IngestLogsRequest request, CancellationToken cancellationToken);
}
