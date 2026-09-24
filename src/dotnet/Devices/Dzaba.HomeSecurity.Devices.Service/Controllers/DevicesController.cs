using Asp.Versioning;
using Dzaba.AspNetUtils;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Services;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.WebApi.Hal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Devices.Service.Controllers;

/// <summary>
/// The network devices seen on a tenant's home network, per
/// docs/architecture/06-notifications.md. Reads need device.view; the one
/// write - rename or acknowledge ("this is mine") - needs device.manage.
/// There is deliberately no POST or DELETE: rows are created only by the
/// ingestion consumer, and departure isn't modelled. HAL, per
/// ADR-0013/ADR-0017: the edit link is only present when the caller can use it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/devices")]
public sealed class DevicesController : HalControllerBase
{
    private readonly IDevicesService devices;
    private readonly IPermissionEvaluator permissionEvaluator;

    public DevicesController(IDevicesService devices, IPermissionEvaluator permissionEvaluator,
        ILinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.devices = devices;
        this.permissionEvaluator = permissionEvaluator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionKeys.DeviceView)]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        var items = new List<Device>();
        await foreach (var device in devices.ListAsync(cancellationToken))
        {
            items.Add(device);
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "Devices", new { orgId }),
        };

        var body = HalEnvelope.WrapCollection("devices", items, links, JsonOptions,
            device => new Dictionary<string, HalLink>
            {
                ["self"] = Links.Build(HttpContext, nameof(Get), "Devices", new { orgId, deviceId = device.Id }),
            });
        return Ok(body);
    }

    [HttpGet("{deviceId:guid}")]
    [Authorize(Policy = PermissionKeys.DeviceView)]
    public async Task<IActionResult> Get(Guid orgId, Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return NotFound();
        }

        return Ok(HalEnvelope.Wrap(device, await BuildLinksAsync(orgId, deviceId, cancellationToken).ConfigureAwait(false), JsonOptions));
    }

    [HttpPatch("{deviceId:guid}")]
    [Authorize(Policy = PermissionKeys.DeviceManage)]
    public async Task<IActionResult> Update(Guid orgId, Guid deviceId, UpdateDevice request, CancellationToken cancellationToken)
    {
        var device = await devices.UpdateAsync(deviceId, request, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return NotFound();
        }

        return Ok(HalEnvelope.Wrap(device, await BuildLinksAsync(orgId, deviceId, cancellationToken).ConfigureAwait(false), JsonOptions));
    }

    private async Task<Dictionary<string, HalLink>> BuildLinksAsync(Guid orgId, Guid deviceId, CancellationToken cancellationToken)
    {
        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Devices", new { orgId, deviceId }),
            ["devices"] = Links.Build(HttpContext, nameof(List), "Devices", new { orgId }),
        };

        // The link's mere presence is the capability check - see
        // docs/architecture/13-hateoas-public-api.md.
        if (await permissionEvaluator.HasPermissionAsync(User.GetUserSubOrNameIdentifier(), orgId, PermissionKeys.DeviceManage, cancellationToken).ConfigureAwait(false))
        {
            links["edit"] = Links.Build(HttpContext, nameof(Update), "Devices", new { orgId, deviceId }, "PATCH");
        }

        return links;
    }
}
