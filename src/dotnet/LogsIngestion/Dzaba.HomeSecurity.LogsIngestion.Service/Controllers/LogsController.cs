using Asp.Versioning;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.DeviceAuth;
using Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;
using Dzaba.HomeSecurity.LogsIngestion.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Controllers;

/// <summary>
/// Kept intentionally thin per docs/architecture/05-agents-and-ingestion.md:
/// validate the device JWT (handled by [Authorize] + JwtBearer middleware),
/// validate/normalize the payload (handled by model binding + [ValidateModel]
/// against the generated contract type), publish to RabbitMQ, return. No
/// processing happens inline - that's the downstream Processing Worker's job.
/// Plain JSON, no HAL envelope - see docs/decisions/0013-hateoas-for-org-api.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = LogsPolicies.LogsWrite)]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/logs")]
public sealed class LogsController : ControllerBase
{
    private readonly IIngestLogsService ingestLogsService;

    public LogsController(IIngestLogsService ingestLogsService)
    {
        ArgumentNullException.ThrowIfNull(ingestLogsService);

        this.ingestLogsService = ingestLogsService;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest(IngestLogsRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();
        var deviceId = User.GetDeviceId();

        await ingestLogsService.IngestAsync(tenantId, deviceId, request, cancellationToken).ConfigureAwait(false);

        return Accepted();
    }
}
