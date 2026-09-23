using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dzaba.HomeSecurity.WebApi.Hal;

internal sealed class LinkFactory : ILinkFactory
{
    private readonly LinkGenerator linkGenerator;

    public LinkFactory(LinkGenerator linkGenerator)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);

        this.linkGenerator = linkGenerator;
    }

    public HalLink Build(HttpContext httpContext, string action, string controller, object? routeValues = null, string? method = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrEmpty(action);
        ArgumentException.ThrowIfNullOrEmpty(controller);

        var path = linkGenerator.GetPathByAction(httpContext, action, controller, routeValues)
            ?? throw new InvalidOperationException($"Could not build a link for {controller}.{action}.");

        return new HalLink(path, method);
    }
}
