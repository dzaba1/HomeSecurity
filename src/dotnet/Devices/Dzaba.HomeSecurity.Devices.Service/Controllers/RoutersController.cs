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
/// Router management from the Admin UI, per
/// docs/architecture/15-router-credentials.md. Reads need router.view
/// (metadata only - the secret is write-only and never returned by any
/// endpoint); writes need router.manage. HAL, per ADR-0013/ADR-0017: the
/// edit/delete/create links are only present when the caller can actually
/// use them.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/routers")]
public sealed class RoutersController : HalControllerBase
{
    private readonly IRoutersService routers;
    private readonly IPermissionEvaluator permissionEvaluator;

    public RoutersController(IRoutersService routers, IPermissionEvaluator permissionEvaluator,
        ILinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(routers);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.routers = routers;
        this.permissionEvaluator = permissionEvaluator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionKeys.RouterView)]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        var items = new List<Router>();
        await foreach (var router in routers.ListAsync(cancellationToken))
        {
            items.Add(router);
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "Routers", new { orgId }),
        };

        if (await CanManageAsync(orgId, cancellationToken).ConfigureAwait(false))
        {
            links["create"] = Links.Build(HttpContext, nameof(Create), "Routers", new { orgId }, "POST");
        }

        var body = HalEnvelope.WrapCollection("routers", items, links, JsonOptions,
            router => new Dictionary<string, HalLink>
            {
                ["self"] = Links.Build(HttpContext, nameof(Get), "Routers", new { orgId, routerId = router.Id }),
            });
        return Ok(body);
    }

    [HttpGet("{routerId:guid}")]
    [Authorize(Policy = PermissionKeys.RouterView)]
    public async Task<IActionResult> Get(Guid orgId, Guid routerId, CancellationToken cancellationToken)
    {
        var router = await routers.GetAsync(routerId, cancellationToken).ConfigureAwait(false);
        if (router is null)
        {
            return NotFound();
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Routers", new { orgId, routerId }),
            ["routers"] = Links.Build(HttpContext, nameof(List), "Routers", new { orgId }),
        };

        // The link's mere presence is the capability check - see
        // docs/architecture/13-hateoas-public-api.md.
        if (await CanManageAsync(orgId, cancellationToken).ConfigureAwait(false))
        {
            links["edit"] = Links.Build(HttpContext, nameof(Update), "Routers", new { orgId, routerId }, "PUT");
            links["delete"] = Links.Build(HttpContext, nameof(Delete), "Routers", new { orgId, routerId }, "DELETE");
        }

        return Ok(HalEnvelope.Wrap(router, links, JsonOptions));
    }

    [HttpPost]
    [Authorize(Policy = PermissionKeys.RouterManage)]
    public async Task<IActionResult> Create(Guid orgId, CreateRouter request, CancellationToken cancellationToken)
    {
        var router = await routers.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Routers", new { orgId, routerId = router.Id }),
            ["routers"] = Links.Build(HttpContext, nameof(List), "Routers", new { orgId }),
            ["edit"] = Links.Build(HttpContext, nameof(Update), "Routers", new { orgId, routerId = router.Id }, "PUT"),
            ["delete"] = Links.Build(HttpContext, nameof(Delete), "Routers", new { orgId, routerId = router.Id }, "DELETE"),
        };

        return CreatedAtAction(nameof(Get), new { orgId, routerId = router.Id }, HalEnvelope.Wrap(router, links, JsonOptions));
    }

    [HttpPut("{routerId:guid}")]
    [Authorize(Policy = PermissionKeys.RouterManage)]
    public async Task<IActionResult> Update(Guid orgId, Guid routerId, UpdateRouter request, CancellationToken cancellationToken)
    {
        var router = await routers.UpdateAsync(routerId, request, cancellationToken).ConfigureAwait(false);
        if (router is null)
        {
            return NotFound();
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Routers", new { orgId, routerId }),
            ["routers"] = Links.Build(HttpContext, nameof(List), "Routers", new { orgId }),
            ["edit"] = Links.Build(HttpContext, nameof(Update), "Routers", new { orgId, routerId }, "PUT"),
            ["delete"] = Links.Build(HttpContext, nameof(Delete), "Routers", new { orgId, routerId }, "DELETE"),
        };

        return Ok(HalEnvelope.Wrap(router, links, JsonOptions));
    }

    [HttpDelete("{routerId:guid}")]
    [Authorize(Policy = PermissionKeys.RouterManage)]
    public async Task<IActionResult> Delete(Guid orgId, Guid routerId, CancellationToken cancellationToken)
    {
        var deleted = await routers.DeleteAsync(routerId, cancellationToken).ConfigureAwait(false);
        return deleted ? NoContent() : NotFound();
    }

    private Task<bool> CanManageAsync(Guid orgId, CancellationToken cancellationToken) =>
        permissionEvaluator.HasPermissionAsync(User.GetUserSubOrNameIdentifier(), orgId, PermissionKeys.RouterManage, cancellationToken);
}
