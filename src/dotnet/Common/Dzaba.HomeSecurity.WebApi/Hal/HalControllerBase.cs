using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.WebApi.Hal;

/// <summary>
/// Shared HAL/link plumbing for every admin-facing API controller
/// (ADR-0013, extended to every admin-facing service by ADR-0017). Public
/// because a base class must be at least as accessible as its (necessarily
/// public, per ASP.NET Core's controller discovery convention) derived
/// controllers.
/// </summary>
public abstract class HalControllerBase : ControllerBase
{
    protected ILinkFactory Links { get; }

    protected JsonSerializerOptions JsonOptions { get; }

    protected HalControllerBase(ILinkFactory links, IOptions<JsonOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        Links = links;
        JsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }
}
