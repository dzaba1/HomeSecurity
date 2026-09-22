using Asp.Versioning;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Org.Contracts;
using Dzaba.HomeSecurity.Org.Service.Hal;
using Dzaba.HomeSecurity.Org.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>The global permission catalog - auth only, not tenant-scoped.</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[HandleErrors]
[Route("api/v{version:apiVersion}/permissions")]
public sealed class PermissionsController : OrgControllerBase
{
    private readonly IPermissionCatalogService catalog;

    public PermissionsController(IPermissionCatalogService catalog, IOrgLinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        this.catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = new List<Permission>();
        await foreach (var permission in catalog.ListAsync(cancellationToken))
        {
            items.Add(permission);
        }

        var links = new Dictionary<string, HalLink> { ["self"] = Links.Build(HttpContext, nameof(List), "Permissions") };
        var body = HalEnvelope.WrapCollection("permissions", items, links, JsonOptions);
        return Ok(body);
    }
}
