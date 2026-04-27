namespace Dzaba.HomeSecurity.LogsIngestion.Contracts.Services
{
    public interface ILogsController
    {
        Task IngestAsync(IngestLogsRequest request);
    }
}
