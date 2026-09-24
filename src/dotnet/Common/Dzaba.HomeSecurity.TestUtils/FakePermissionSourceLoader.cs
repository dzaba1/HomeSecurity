using System.Collections.Concurrent;
using Dzaba.HomeSecurity.Authorization;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// Stands in for the Org.Service-backed IPermissionSourceLoader (see
/// Dzaba.HomeSecurity.Authorization.OrgApi) in a service's controller tests:
/// a user is a member of exactly the tenants they've been granted something
/// in, and holds exactly the permission keys granted there. It also counts
/// its calls and can simulate Org.Service being down, so tests can check
/// what the access-context cache does and doesn't shield a service from.
/// </summary>
public sealed class FakePermissionSourceLoader : IPermissionSourceLoader
{
    private readonly ConcurrentDictionary<(Guid TenantId, string UserId), string[]> grants = new();
    private readonly object stateLock = new();
    private int loadCount;
    private bool unavailable;

    /// <summary>How many times the source was actually asked, i.e. how many cache misses there were.</summary>
    public int LoadCount
    {
        get
        {
            lock (stateLock)
            {
                return loadCount;
            }
        }
    }

    /// <summary>When true, every load fails the way an unreachable Org.Service would.</summary>
    public bool Unavailable
    {
        get
        {
            lock (stateLock)
            {
                return unavailable;
            }
        }
        set
        {
            lock (stateLock)
            {
                unavailable = value;
            }
        }
    }

    /// <summary>Makes <paramref name="userId"/> a member of <paramref name="tenantId"/> holding exactly these keys.</summary>
    public void Grant(Guid tenantId, string userId, params string[] permissionKeys) =>
        grants[(tenantId, userId)] = permissionKeys;

    public Task<AccessContext> LoadAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        lock (stateLock)
        {
            loadCount++;
            if (unavailable)
            {
                throw new HttpRequestException("Org.Service is unavailable (simulated).");
            }
        }

        return Task.FromResult(grants.TryGetValue((tenantId, userId), out var keys)
            ? new AccessContext(true, keys)
            : new AccessContext(false, []));
    }
}
