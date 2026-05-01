using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.LogsIngestion.Auth;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.LogsIngestion.Contracts.Services;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.LogsIngestion.Controllers;

[ApiController]
[Route("api/v1/logs")]
[HandleErrors]
public class LogsController : ControllerBase, ILogsController
{
    private readonly IEventPublisher eventPublisher;
    private readonly AuthDbContext authDbContext;

    public LogsController(ILogger<LogsController> logger,
        IEventPublisher eventPublisher,
        AuthDbContext authDbContext)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(authDbContext);

        this.eventPublisher = eventPublisher;
        this.authDbContext = authDbContext;
    }

    [HttpPost]
    [ValidateModel]
    [Authorize]
    public async Task IngestAsync([Required][FromBody] IngestLogsRequest request)
    {
        var userName = User.Identity?.Name;
        var userModel = await authDbContext.Users.FirstOrDefaultAsync(u => u.Name == userName).ConfigureAwait(false);

        foreach (var evt in request.Events)
        {
            await eventPublisher.PublishAsync(evt).ConfigureAwait(false);
        }
    }
}
