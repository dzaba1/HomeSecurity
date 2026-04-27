using Dzaba.BasicAuthentication;
using System.Security.Claims;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth
{
    internal sealed class BasicAuthHandler : IBasicAuthenticationHandlerService
    {
        public async Task AddClaimsAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext, ICollection<Claim> claims, object context)
        {
            
        }

        public async Task<CheckPasswordResult> CheckPasswordAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext)
        {
            throw new NotImplementedException();
        }

        public async Task HandleUnauthorizedAsync(HttpContext httpContext, string failReason)
        {
            await httpContext.Response.WriteAsync(failReason).ConfigureAwait(false);
        }
    }
}
