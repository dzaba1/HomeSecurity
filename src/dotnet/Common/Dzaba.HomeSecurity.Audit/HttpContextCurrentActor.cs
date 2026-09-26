using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Audit.Contracts;
using Dzaba.HomeSecurity.DeviceAuth;
using Microsoft.AspNetCore.Http;

namespace Dzaba.HomeSecurity.Audit;

internal sealed class HttpContextCurrentActor : ICurrentActor
{
    internal const string SystemActorId = "system";

    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpContextCurrentActor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        this.httpContextAccessor = httpContextAccessor;
    }

    public Actor GetCurrent()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new Actor { Type = ActorType.System, Id = SystemActorId };
        }

        // A device token carries device_id; a human's Keycloak token never
        // does, so its presence is what tells the two apart.
        var deviceId = user.FindFirst(DeviceClaimTypes.DeviceId)?.Value;
        if (!string.IsNullOrEmpty(deviceId))
        {
            return new Actor { Type = ActorType.Device, Id = deviceId };
        }

        var userId = user.GetUserSubOrNameIdentifier();
        if (string.IsNullOrEmpty(userId))
        {
            // Attributing an authenticated action to "system" would put a
            // false statement in the audit trail.
            throw new InvalidOperationException("The authenticated principal has no subject to record as the audit actor.");
        }

        return new Actor { Type = ActorType.User, Id = userId };
    }
}
