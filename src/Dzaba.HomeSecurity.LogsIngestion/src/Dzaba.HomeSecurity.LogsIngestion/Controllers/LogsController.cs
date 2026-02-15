using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.LogsIngestion.Controllers;

[ApiController]
[Route("api/v1/logs")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> logger;
    private readonly IEventPublisher eventPublisher;

    public LogsController(ILogger<LogsController> logger,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.logger = logger;
        this.eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([Required][FromBody] IngestLogsRequest request)
    {
        try
        {
            foreach (var evt in request.Events)
            {
                await eventPublisher.PublishAsync(evt).ConfigureAwait(false);
            }

            return Accepted();
        }
        catch (HttpResultException ex)
        {
            logger.LogWarning(ex, "Request failed with HTTP {HttpCode}: {Message}", (int)ex.HttpCode, ex.Message);
            return StatusCode((int)ex.HttpCode, ex.Message);
        }
    }
}
