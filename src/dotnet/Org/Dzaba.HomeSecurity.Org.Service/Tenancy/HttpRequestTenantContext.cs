namespace Dzaba.HomeSecurity.Org.Service.Tenancy;

/// <summary>
/// One scoped instance registered behind both <see cref="Domain.ITenantContext"/>
/// (read side, consumed by <c>AppDbContext</c> and application code) and
/// <see cref="IMutableTenantContext"/> (write side, used only by
/// <see cref="TenantResolutionMiddleware"/> and org creation). Reading
/// before it's set is a bug, not a recoverable condition - the pipeline
/// order in Program.cs guarantees it's always set first.
/// </summary>
internal sealed class HttpRequestTenantContext : IMutableTenantContext
{
    private Guid? tenantId;

    public Guid TenantId => tenantId ??
        throw new InvalidOperationException(
            $"{nameof(TenantId)} was read before {nameof(SetTenantId)} was called for this request.");

    public void SetTenantId(Guid tenantId)
    {
        this.tenantId = tenantId;
    }
}
