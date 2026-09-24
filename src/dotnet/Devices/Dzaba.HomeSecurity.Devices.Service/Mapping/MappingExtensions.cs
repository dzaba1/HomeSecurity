using Dzaba.HomeSecurity.Devices.Contracts;
using RouterEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.Router;
using RouterProtocolEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.RouterProtocol;
using RouterAuthModeEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.RouterAuthMode;
using Router = Dzaba.HomeSecurity.Devices.Contracts.Router;

namespace Dzaba.HomeSecurity.Devices.Service.Mapping;

/// <summary>Entity-to-contract mapping for every resource the Devices API exposes.</summary>
internal static class MappingExtensions
{
    /// <summary>Deliberately drops the encrypted secret: it is write-only, never returned.</summary>
    public static Router ToContract(this RouterEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new Router
        {
            Id = entity.Id,
            Name = entity.Name,
            Host = entity.Host,
            Protocol = ConvertByName<Router_protocol>(entity.Protocol),
            AuthMode = entity.AuthMode is { } authMode ? ConvertByName<Router_auth_mode>(authMode) : null,
            Username = entity.Username,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public static RouterProtocolEntity ToEntity(this Router_protocol protocol) =>
        ConvertByName<RouterProtocolEntity>(protocol);

    public static RouterAuthModeEntity ToEntity(this Router_auth_mode authMode) =>
        ConvertByName<RouterAuthModeEntity>(authMode);

    // The generated contract enums and the entity enums share value names
    // (WebScrape, SNMP, HttpBasic, ...) but are separate types; converting by
    // name rather than by underlying number means a renamed or reordered
    // member fails loudly here instead of silently mapping to the wrong one.
    private static TTo ConvertByName<TTo>(Enum value)
        where TTo : struct, Enum =>
        Enum.Parse<TTo>(value.ToString());
}
