namespace Dzaba.HomeSecurity.Org.Service.Hal;

/// <summary>
/// Centralizes href construction via ASP.NET Core's own built-in
/// LinkGenerator, so controllers never string-concatenate a URL by hand -
/// only the surrounding _links/_embedded envelope (HalEnvelope) is custom.
/// </summary>
public interface IOrgLinkFactory
{
    HalLink Build(HttpContext httpContext, string action, string controller, object? routeValues = null, string? method = null);
}
