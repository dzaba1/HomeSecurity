using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Mapping;
using Dzaba.HomeSecurity.Devices.Service.Security;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

internal sealed class AgentRouterConfigService : IAgentRouterConfigService
{
    private readonly DbContextOptions<DevicesDbContext> dbOptions;
    private readonly IRouterSecretProtector secretProtector;
    private readonly ILogger<AgentRouterConfigService> logger;

    public AgentRouterConfigService(DbContextOptions<DevicesDbContext> dbOptions, IRouterSecretProtector secretProtector,
        ILogger<AgentRouterConfigService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbOptions);
        ArgumentNullException.ThrowIfNull(secretProtector);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbOptions = dbOptions;
        this.secretProtector = secretProtector;
        this.logger = logger;
    }

    public async Task<RouterConfig?> GetAsync(Guid tenantId, Guid deviceCredentialId, CancellationToken cancellationToken)
    {
        // The agent endpoint has no {orgId} segment, so the tenant-resolution
        // middleware never sets a tenant for it. The context is bound to the
        // tenant claimed by the validated device token instead - the same
        // "fresh context bound to a claimed tenant, not the ambient one"
        // pattern the permission evaluator uses. Its query filters then keep
        // both lookups inside that tenant.
        using var db = new DevicesDbContext(dbOptions, new StaticTenantContext(tenantId));

        var binding = await db.AgentRouterBindings
            .FirstOrDefaultAsync(b => b.DeviceCredentialId == deviceCredentialId, cancellationToken).ConfigureAwait(false);
        if (binding is null)
        {
            logger.LogInformation("Device {DeviceId} in tenant {TenantId} asked for its router config but has no router paired",
                deviceCredentialId, tenantId);
            return null;
        }

        var router = await db.Routers
            .FirstOrDefaultAsync(r => r.Id == binding.RouterId, cancellationToken).ConfigureAwait(false);
        if (router is null)
        {
            // Deleting a router removes its bindings, so this is a broken
            // reference rather than an expected state.
            logger.LogWarning("Device {DeviceId} in tenant {TenantId} is bound to missing router {RouterId}",
                deviceCredentialId, tenantId, binding.RouterId);
            return null;
        }

        var config = router.ToRouterConfig(secretProtector.Unprotect(tenantId, router.EncryptedSecret));

        // An audit trail of who was handed a router credential - the secret
        // itself is deliberately never logged.
        logger.LogInformation("Router {RouterId} config handed to device {DeviceId} in tenant {TenantId}",
            router.Id, deviceCredentialId, tenantId);

        return config;
    }
}
