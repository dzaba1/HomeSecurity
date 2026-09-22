using System.Text.Json;
using System.Text.Json.Nodes;
using Asp.Versioning;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Org.Service.Hal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// The discovery entry point - a client that starts here and only follows
/// links never needs a hardcoded URL beyond this one, per
/// docs/architecture/13-hateoas-public-api.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[HandleErrors]
[Route("api/v{version:apiVersion}")]
public sealed class RootController : OrgControllerBase
{
    public RootController(IOrgLinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
    }

    [HttpGet]
    public IActionResult Index()
    {
        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Index), "Root"),
            ["orgs"] = Links.Build(HttpContext, nameof(OrgsController.ListMine), "Orgs"),
            ["permissions"] = Links.Build(HttpContext, nameof(PermissionsController.List), "Permissions"),
        };

        var body = new JsonObject { ["_links"] = JsonSerializer.SerializeToNode(links, JsonOptions) };
        return Ok(body);
    }
}
