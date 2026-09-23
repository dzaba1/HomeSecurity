using System.Text.Json;
using Dzaba.HomeSecurity.WebApi.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// Shared HAL/link plumbing for every Org API controller. Public because a
/// base class must be at least as accessible as its (necessarily public,
/// per ASP.NET Core's controller discovery convention) derived controllers.
/// </summary>
public abstract class OrgControllerBase : ControllerBase
{
    protected ILinkFactory Links { get; }

    protected JsonSerializerOptions JsonOptions { get; }

    protected OrgControllerBase(ILinkFactory links, IOptions<JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        Links = links;
        JsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }
}
