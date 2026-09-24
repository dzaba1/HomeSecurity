using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Dzaba.HomeSecurity.Devices.Service.Security;

/// <summary>
/// Uses the ASP.NET Core Data Protection API - a ready-made framework
/// component, not custom crypto. Each tenant gets its own protector (a
/// sub-purpose keyed by the tenant id), so a ciphertext copied from one
/// tenant's row to another's can't be decrypted there.
/// </summary>
internal sealed class RouterSecretProtector : IRouterSecretProtector
{
    // Versioned so a future change of scheme can tell old ciphertext from new.
    private const string Purpose = "Dzaba.HomeSecurity.Devices.Router.Secret.v1";

    private readonly IDataProtectionProvider dataProtectionProvider;

    public RouterSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        this.dataProtectionProvider = dataProtectionProvider;
    }

    public byte[] Protect(Guid tenantId, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        return CreateProtector(tenantId).Protect(Encoding.UTF8.GetBytes(secret));
    }

    public string Unprotect(Guid tenantId, byte[] protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);

        return Encoding.UTF8.GetString(CreateProtector(tenantId).Unprotect(protectedSecret));
    }

    private IDataProtector CreateProtector(Guid tenantId) =>
        dataProtectionProvider.CreateProtector(Purpose).CreateProtector(tenantId.ToString("N"));
}
