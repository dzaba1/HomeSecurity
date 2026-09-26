using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Mapping;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Device = Dzaba.HomeSecurity.Devices.Contracts.Device;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

internal sealed class DevicesService : IDevicesService
{
    private readonly DevicesDbContext db;
    private readonly ITenantContext tenantContext;
    private readonly IDomainEventPublisher eventPublisher;
    private readonly ILogger<DevicesService> logger;

    public DevicesService(DevicesDbContext db, ITenantContext tenantContext, IDomainEventPublisher eventPublisher,
        ILogger<DevicesService> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        this.db = db;
        this.tenantContext = tenantContext;
        this.eventPublisher = eventPublisher;
        this.logger = logger;
    }

    public async IAsyncEnumerable<Device> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Most recently seen first - the newest arrivals are what a user
        // checking for an unfamiliar device wants at the top.
        var query = db.Devices.OrderByDescending(d => d.LastSeenAt).ThenBy(d => d.MacAddress);
        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    public async Task<Device?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var entity = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken).ConfigureAwait(false);
        return entity?.ToContract();
    }

    public async Task<Device?> UpdateAsync(Guid deviceId, UpdateDevice request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name is null && request.Status is null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest, "At least one of name or status must be supplied.");
        }

        var entity = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        // Before-values, captured ahead of the overwrite below.
        var previousName = entity.Name;
        var previousStatus = entity.Status;

        if (request.Name is not null)
        {
            // An empty name clears it.
            entity.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        }

        if (request.Status is { } status)
        {
            entity.Status = status.ToEntity();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = tenantContext.TenantId;
        logger.LogInformation("Device {DeviceId} updated in tenant {TenantId}, status is now {Status}",
            deviceId, tenantId, entity.Status);

        // One event per PATCH, which may rename, acknowledge (status
        // Unknown -> Known) or both: the changes map says which. The MAC
        // address is personal data and stays out - the target id identifies
        // the device.
        var changes = new Dictionary<string, object?>();
        AddChange(changes, "name", previousName, entity.Name);
        AddChange(changes, "status", previousStatus.ToString(), entity.Status.ToString());
        await eventPublisher.PublishAsync(DomainEventNames.DeviceUpdated, DomainEventTargetTypes.Device, entity.Id.ToString(),
            new Dictionary<string, object?> { ["changes"] = changes },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return entity.ToContract();
    }

    private static void AddChange(Dictionary<string, object?> changes, string field, string? from, string? to)
    {
        if (from != to)
        {
            changes[field] = new Dictionary<string, object?> { ["from"] = from, ["to"] = to };
        }
    }
}
