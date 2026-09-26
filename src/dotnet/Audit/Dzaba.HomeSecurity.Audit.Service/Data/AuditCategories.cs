using Dzaba.HomeSecurity.Audit.Service.Data.Entities;

namespace Dzaba.HomeSecurity.Audit.Service.Data;

internal static class AuditCategories
{
    /// <summary>
    /// The category of an event, from the first segment of its dotted name
    /// (<c>role.assigned</c> is Access). An unrecognised prefix is
    /// <see cref="AuditCategory.Other"/> rather than an error: an event from a
    /// service this list hasn't learned about yet must still be recorded.
    /// </summary>
    public static AuditCategory For(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var separator = action.IndexOf('.', StringComparison.Ordinal);
        var prefix = separator < 0 ? action : action[..separator];

        return prefix switch
        {
            "identity" => AuditCategory.Identity,
            "organization" or "membership" or "role" => AuditCategory.Access,
            "router" => AuditCategory.Router,
            "device" => AuditCategory.Device,
            "dsr" => AuditCategory.DataSubjectRequest,
            _ => AuditCategory.Other
        };
    }
}
