using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Devices.Contracts;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Mapping;
using Dzaba.HomeSecurity.Devices.Service.Messages;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.EntityFrameworkCore;
using Device = Dzaba.HomeSecurity.Devices.Contracts.Device;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

internal sealed class DevicesService : IDevicesService
{
    private readonly DevicesDbContext db;
    private readonly ITenantContext tenantContext;
    private readonly IMessageBus messageBus;
    private readonly ILogger<DevicesService> logger;

    public DevicesService(DevicesDbContext db, ITenantContext tenantContext, IMessageBus messageBus,
        ILogger<DevicesService> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(logger);

        this.db = db;
        this.tenantContext = tenantContext;
        this.messageBus = messageBus;
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

        // Notification only, not event-carried state transfer - see
        // device_changed_message.json's own description. No consumer exists
        // yet; published as a low-cost hedge for future integrations.
        await messageBus.PublishAsync(RoutingKeys.DeviceChanged,
            new DeviceChangedMessage
            {
                TenantId = tenantId,
                DeviceId = entity.Id,
                MacAddress = entity.MacAddress,
                Status = entity.Status.ToContract(),
                ChangedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);

        return entity.ToContract();
    }
}
