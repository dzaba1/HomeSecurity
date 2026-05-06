using Microsoft.AspNetCore.Http;

namespace Dzaba.HomeSecurity.Org.Contracts;

public interface IOrgService
{
    Task<string> GetTenantIdAsync(HttpContext context);
    Task<bool> HasAccessAsync(string userId, string id);
    Task<string> CreateOrgAsync(string name);
}
