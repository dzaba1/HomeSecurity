using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.LogsIngestion.Controllers;

[ApiController]
[Route("api/v1/logs")]
[HandleErrors]
public class LogsController : ControllerBase
{
    private readonly IEventPublisher eventPublisher;

    public LogsController(ILogger<LogsController> logger,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.eventPublisher = eventPublisher;
    }

    [HttpPost]
    [ValidateModel]
    public async Task Ingest([Required][FromBody] IngestLogsRequest request)
    {
        foreach (var evt in request.Events)
        {
            await eventPublisher.PublishAsync(evt).ConfigureAwait(false);
        }
    }
}
