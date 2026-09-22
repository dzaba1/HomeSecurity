using Dzaba.HomeSecurity.Org.Contracts;

namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>
/// Operates within the already-resolved ambient tenant. Listing/reading
/// sees both system-default roles (TenantId null) and this tenant's own
/// custom roles - the existing EF Core query filter already covers that.
/// </summary>
public interface IRolesService
{
    IAsyncEnumerable<Role> ListAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetAsync(Guid roleId, CancellationToken cancellationToken);

    Task<Role> CreateAsync(CreateRole request, CancellationToken cancellationToken);

    /// <summary>Full replace of a custom role's name/permission set. Throws if the role is a system role.</summary>
    Task<Role?> UpdateAsync(Guid roleId, CreateRole request, CancellationToken cancellationToken);

    /// <summary>Throws if the role is a system role.</summary>
    Task<bool> DeleteAsync(Guid roleId, CancellationToken cancellationToken);
}
