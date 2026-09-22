using System.Text.Json;
using Dzaba.HomeSecurity.Org.Service.Hal;
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
    protected IOrgLinkFactory Links { get; }

    protected JsonSerializerOptions JsonOptions { get; }

    protected OrgControllerBase(IOrgLinkFactory links, IOptions<JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        Links = links;
        JsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }
}
