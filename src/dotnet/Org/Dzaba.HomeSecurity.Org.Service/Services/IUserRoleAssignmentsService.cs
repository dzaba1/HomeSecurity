using Dzaba.HomeSecurity.Org.Contracts;

namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>Operates within the already-resolved ambient tenant.</summary>
public interface IUserRoleAssignmentsService
{
    IAsyncEnumerable<UserRoleAssignment> ListAsync(CancellationToken cancellationToken = default);

    Task<UserRoleAssignment> AssignAsync(AssignUserRole request, CancellationToken cancellationToken);

    Task<bool> UnassignAsync(string userId, Guid roleId, CancellationToken cancellationToken);
}
