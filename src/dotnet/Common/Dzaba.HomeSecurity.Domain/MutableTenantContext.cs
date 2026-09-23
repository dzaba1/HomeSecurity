namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// One scoped instance is registered behind both <see cref="ITenantContext"/>
/// (read side) and <see cref="IMutableTenantContext"/> (write side). Reading
/// before it's set is a bug, not a recoverable condition - the pipeline
/// order guarantees the tenant-resolution middleware sets it first.
/// </summary>
public sealed class MutableTenantContext : IMutableTenantContext
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
