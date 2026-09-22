using OrganizationEntity = Dzaba.HomeSecurity.Data.Entities.Organization;
using MembershipEntity = Dzaba.HomeSecurity.Data.Entities.Membership;
using RoleEntity = Dzaba.HomeSecurity.Data.Entities.Role;
using PermissionEntity = Dzaba.HomeSecurity.Data.Entities.Permission;
using UserRoleEntity = Dzaba.HomeSecurity.Data.Entities.UserRole;
using Organization = Dzaba.HomeSecurity.Org.Contracts.Organization;
using Membership = Dzaba.HomeSecurity.Org.Contracts.Membership;
using Role = Dzaba.HomeSecurity.Org.Contracts.Role;
using Permission = Dzaba.HomeSecurity.Org.Contracts.Permission;
using UserRoleAssignment = Dzaba.HomeSecurity.Org.Contracts.UserRoleAssignment;

namespace Dzaba.HomeSecurity.Org.Service.Mapping;

/// <summary>Entity-to-contract mapping for every resource the Org API exposes.</summary>
internal static class MappingExtensions
{
    public static Organization ToContract(this OrganizationEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new Organization { Id = entity.Id, Identifier = entity.Identifier, Name = entity.Name };
    }

    public static Membership ToContract(this MembershipEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new Membership { OrganizationId = entity.OrganizationId, UserId = entity.UserId };
    }

    /// <summary>Requires <see cref="RoleEntity.RolePermissions"/> to already be loaded.</summary>
    public static Role ToContract(this RoleEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new Role
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            PermissionKeys = entity.RolePermissions.Select(rp => rp.PermissionKey).ToList(),
        };
    }

    public static Permission ToContract(this PermissionEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new Permission { Key = entity.Key, Description = entity.Description };
    }

    public static UserRoleAssignment ToContract(this UserRoleEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new UserRoleAssignment { UserId = entity.UserId, TenantId = entity.TenantId, RoleId = entity.RoleId };
    }
}
