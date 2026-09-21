using Microsoft.AspNetCore.Http;

namespace Dzaba.HomeSecurity.Auth;

public static class Extensions
{
    public static string GetOrganizationId(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.TryGetValue(Org.Contracts.Constants.OrgHeaderName, out var orgId))
        {
            return orgId;
        }
        return null;
    }
}
