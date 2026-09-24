using System.Collections.Concurrent;
using Dzaba.HomeSecurity.Authorization;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// Stands in for the Org.Service-backed IPermissionSourceLoader (see
/// Dzaba.HomeSecurity.Authorization.OrgApi) in a service's controller tests:
/// a user is a member of exactly the tenants they've been granted something
/// in, and holds exactly the permission keys granted there.
/// </summary>
public sealed class FakePermissionSourceLoader : IPermissionSourceLoader
{
    private readonly ConcurrentDictionary<(Guid TenantId, string UserId), string[]> grants = new();

    /// <summary>Makes <paramref name="userId"/> a member of <paramref name="tenantId"/> holding exactly these keys.</summary>
    public void Grant(Guid tenantId, string userId, params string[] permissionKeys) =>
        grants[(tenantId, userId)] = permissionKeys;

    public Task<AccessContext> LoadAsync(Guid tenantId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(grants.TryGetValue((tenantId, userId), out var keys)
            ? new AccessContext(true, keys)
            : new AccessContext(false, []));
}
