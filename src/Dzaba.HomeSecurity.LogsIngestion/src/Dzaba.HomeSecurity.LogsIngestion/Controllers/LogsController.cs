using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using System.Net;

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

    private IngestLogsRequest GetRequest()
    {
        try
        {
            return IngestLogsRequest.Parser.ParseFrom(Request.Body);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new HttpResultException(HttpStatusCode.BadRequest, "Invalid protobuf payload", ex);
        }
    }

    [HttpPost("ingest")]
    [Consumes("application/x-protobuf")]
    public async Task<IActionResult> Ingest(CancellationToken ct)
    {
        try
        {
            var requestBody = GetRequest();

            if (requestBody.Events.Count == 0)
            {
                return BadRequest("Empty batch");
            }

            foreach (var evt in requestBody.Events)
            {
                await eventPublisher.PublishAsync(evt).ConfigureAwait(false);
            }

            var response = new IngestLogsResponse();

            return Accepted(response);
        }
        catch (HttpResultException ex)
        {
            logger.LogWarning(ex, "Request failed with HTTP {HttpCode}: {Message}", (int)ex.HttpCode, ex.Message);
            return StatusCode((int)ex.HttpCode, ex.Message);
        }
    }
}
