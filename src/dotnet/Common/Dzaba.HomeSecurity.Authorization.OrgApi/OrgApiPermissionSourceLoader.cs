using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.Authorization.OrgApi;

/// <summary>
/// For a service that doesn't own Membership/UserRole/RolePermission
/// directly (they live in Org.Service's database, see
/// docs/decisions/0016-devices-service-owns-its-own-database.md): on a
/// cache miss it asks Org.Service's access-context endpoint instead of a
/// local table. The caller's own bearer token is forwarded as-is ("token
/// relay"), so no service-account credentials or extra auth scheme exist -
/// and Org.Service always answers for the token's own subject, never a
/// client-supplied user id. That's why <c>userId</c> is only meaningful as
/// the cache key here: this loader must only be used for the authenticated
/// caller of the current request, never to look up some other user.
/// </summary>
internal sealed class OrgApiPermissionSourceLoader : IPermissionSourceLoader
{
    private readonly HttpClient httpClient;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<OrgApiPermissionSourceLoader> logger;

    public OrgApiPermissionSourceLoader(HttpClient httpClient, IHttpContextAccessor httpContextAccessor,
        ILogger<OrgApiPermissionSourceLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        this.httpClient = httpClient;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    public async Task<AccessContext> LoadAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization))
        {
            throw new InvalidOperationException(
                "There is no incoming bearer token to relay to Org.Service's access-context endpoint.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/orgs/{tenantId}/access-context");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Org.Service's tenant-resolution middleware answers 404, not 403,
            // for a non-member - same "don't reveal existence" rule as here.
            logger.LogDebug("Org.Service reports user {UserId} is not a member of tenant {TenantId}", userId, tenantId);
            return new AccessContext(false, []);
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AccessContextResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Org.Service's access-context endpoint returned an empty body.");

        return new AccessContext(true, [.. body.PermissionKeys]);
    }
}
