using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Auth.ActionFilters;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.LogsIngestion.Contracts.Services;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.LogsIngestion.Controllers;

[ApiController]
[Route("api/v1/logs")]
[HandleErrors]
[ValidateTenant]
public class LogsController : ControllerBase, ILogsController
{
    private readonly IEventPublisher eventPublisher;

    public LogsController(ILogger<LogsController> logger,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        this.eventPublisher = eventPublisher;
    }

    [HttpPost]
    [ValidateModel]
    [Authorize]
    public async Task IngestAsync([Required][FromBody] IngestLogsRequest request)
    {
        // TODO
        int homeId = 0;

        foreach (var evt in request.Events)
        {
            await eventPublisher.PublishAsync(homeId, evt).ConfigureAwait(false);
        }
    }
}
