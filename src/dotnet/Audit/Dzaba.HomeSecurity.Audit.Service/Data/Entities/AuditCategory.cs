namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// The kind of thing an event is about, derived from its name. It is what
/// retention windows are configured per (docs/architecture/16-auditing-and-compliance.md
/// section 4) and, with the tenant, what a hash chain is scoped to - so purging
/// a short-retention category never has to skip over a long-retention event.
/// </summary>
internal enum AuditCategory
{
    Identity,
    Access,
    Router,
    Device,
    DataSubjectRequest,
    Other
}
