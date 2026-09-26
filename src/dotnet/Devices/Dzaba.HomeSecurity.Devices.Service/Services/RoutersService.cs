using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Mapping;
using Dzaba.HomeSecurity.Devices.Service.Security;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using Microsoft.EntityFrameworkCore;
using RouterEntity = Dzaba.HomeSecurity.Devices.Service.Data.Entities.Router;
using Router = Dzaba.HomeSecurity.Devices.Contracts.Router;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

internal sealed class RoutersService : IRoutersService
{
    private readonly DevicesDbContext db;
    private readonly ITenantContext tenantContext;
    private readonly IRouterSecretProtector secretProtector;
    private readonly IDomainEventPublisher eventPublisher;
    private readonly ILogger<RoutersService> logger;

    public RoutersService(DevicesDbContext db, ITenantContext tenantContext, IRouterSecretProtector secretProtector,
        IDomainEventPublisher eventPublisher, ILogger<RoutersService> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(secretProtector);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        this.db = db;
        this.tenantContext = tenantContext;
        this.secretProtector = secretProtector;
        this.eventPublisher = eventPublisher;
        this.logger = logger;
    }

    public async IAsyncEnumerable<Router> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entity in db.Routers.OrderBy(r => r.Name).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    public async Task<Router?> GetAsync(Guid routerId, CancellationToken cancellationToken)
    {
        var entity = await db.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken).ConfigureAwait(false);
        return entity?.ToContract();
    }

    public async Task<Router> CreateAsync(CreateRouter request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConnectionSettings(request.Protocol, request.AuthMode, request.Username);

        var tenantId = tenantContext.TenantId;
        var entity = new RouterEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Host = request.Host,
            Protocol = request.Protocol.ToEntity(),
            AuthMode = request.AuthMode?.ToEntity(),
            Username = request.Username,
            EncryptedSecret = secretProtector.Protect(tenantId, request.Secret),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Routers.Add(entity);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Router {RouterId} created in tenant {TenantId}", entity.Id, tenantId);

        // Extended data is an explicit whitelist: the request also holds the
        // secret, so nothing is ever built from the request or entity itself.
        await eventPublisher.PublishAsync(DomainEventNames.RouterCreated, DomainEventTargetTypes.Router, entity.Id.ToString(),
            new Dictionary<string, object?>
            {
                ["name"] = entity.Name,
                ["host"] = entity.Host,
                ["protocol"] = entity.Protocol.ToString(),
                ["authMode"] = entity.AuthMode?.ToString(),
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return entity.ToContract();
    }

    public async Task<Router?> UpdateAsync(Guid routerId, UpdateRouter request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConnectionSettings(request.Protocol, request.AuthMode, request.Username);

        var entity = await db.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var tenantId = tenantContext.TenantId;

        // Before-values, captured ahead of the overwrite below.
        var (previousName, previousHost, previousProtocol, previousAuthMode, previousUsername) =
            (entity.Name, entity.Host, entity.Protocol, entity.AuthMode, entity.Username);

        entity.Name = request.Name;
        entity.Host = request.Host;
        entity.Protocol = request.Protocol.ToEntity();
        entity.AuthMode = request.AuthMode?.ToEntity();
        entity.Username = request.Username;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Only re-encrypted when a new secret is supplied - otherwise the
        // stored ciphertext is left exactly as it was.
        if (request.Secret is not null)
        {
            entity.EncryptedSecret = secretProtector.Protect(tenantId, request.Secret);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Router {RouterId} updated in tenant {TenantId} (secret rotated: {SecretRotated})",
            routerId, tenantId, request.Secret is not null);

        // Only what changed, as from/to. The login name and the secret are
        // reported as booleans only: that they changed matters, their values
        // never travel.
        var changes = new Dictionary<string, object?>();
        AddChange(changes, "name", previousName, entity.Name);
        AddChange(changes, "host", previousHost, entity.Host);
        AddChange(changes, "protocol", previousProtocol.ToString(), entity.Protocol.ToString());
        AddChange(changes, "authMode", previousAuthMode?.ToString(), entity.AuthMode?.ToString());
        await eventPublisher.PublishAsync(DomainEventNames.RouterUpdated, DomainEventTargetTypes.Router, routerId.ToString(),
            new Dictionary<string, object?>
            {
                ["changes"] = changes,
                ["usernameChanged"] = previousUsername != entity.Username,
                ["secretRotated"] = request.Secret is not null,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return entity.ToContract();
    }

    public async Task<bool> DeleteAsync(Guid routerId, CancellationToken cancellationToken)
    {
        var entity = await db.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        // Loaded and handled explicitly rather than relying on the database's
        // FK cascade/set-null: that's real behavior on Postgres, but EF Core
        // only applies it through the change tracker for rows it has actually
        // loaded, so relying on it would silently depend on which
        // IDbServerProvider is configured. Removing the bindings is also what
        // immediately ends any agent's access to this router's credential.
        var bindings = await db.AgentRouterBindings
            .Where(b => b.RouterId == routerId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        db.AgentRouterBindings.RemoveRange(bindings);

        foreach (var device in await db.Devices
            .Where(d => d.LastSeenViaRouterId == routerId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false))
        {
            device.LastSeenViaRouterId = null;
        }

        var tenantId = tenantContext.TenantId;
        db.Routers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Router {RouterId} deleted from tenant {TenantId}", routerId, tenantId);

        await eventPublisher.PublishAsync(DomainEventNames.RouterDeleted, DomainEventTargetTypes.Router, routerId.ToString(),
            new Dictionary<string, object?>
            {
                ["name"] = entity.Name,
                ["host"] = entity.Host,
                ["revokedAgentBindings"] = bindings.Length,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    // WebScrape needs an auth mode and a login; SNMP has neither (its secret
    // is a community string) - see docs/architecture/15-router-credentials.md.
    private static void ValidateConnectionSettings(Router_protocol protocol, Router_auth_mode? authMode, string username)
    {
        switch (protocol)
        {
            case Router_protocol.WebScrape:
                if (authMode is null)
                {
                    throw new HttpResponseException(HttpStatusCode.BadRequest, "authMode is required when protocol is WebScrape.");
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    throw new HttpResponseException(HttpStatusCode.BadRequest, "username is required when protocol is WebScrape.");
                }

                break;

            case Router_protocol.SNMP:
                if (authMode is not null)
                {
                    throw new HttpResponseException(HttpStatusCode.BadRequest, "authMode must be omitted when protocol is SNMP.");
                }

                break;
        }
    }

    private static void AddChange(Dictionary<string, object?> changes, string field, string? from, string? to)
    {
        if (from != to)
        {
            changes[field] = new Dictionary<string, object?> { ["from"] = from, ["to"] = to };
        }
    }
}
