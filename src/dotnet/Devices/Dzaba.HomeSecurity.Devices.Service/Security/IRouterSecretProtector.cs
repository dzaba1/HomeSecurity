namespace Dzaba.HomeSecurity.Devices.Service.Security;

/// <summary>
/// Encrypts/decrypts a router's password or SNMP community string for
/// storage (docs/architecture/15-router-credentials.md): the plaintext
/// never reaches the database.
/// </summary>
internal interface IRouterSecretProtector
{
    byte[] Protect(Guid tenantId, string secret);

    string Unprotect(Guid tenantId, byte[] protectedSecret);
}
