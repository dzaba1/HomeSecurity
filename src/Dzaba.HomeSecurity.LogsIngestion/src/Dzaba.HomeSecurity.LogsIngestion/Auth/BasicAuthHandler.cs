using Dzaba.BasicAuthentication;
using System.Security.Claims;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

internal sealed class BasicAuthHandler : IBasicAuthenticationHandlerService
{
    private readonly IPasswordHasher passwordHasher;

    public BasicAuthHandler(IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);

        this.passwordHasher = passwordHasher;
    }

    public async Task AddClaimsAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext, ICollection<Claim> claims, object context)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        throw new NotImplementedException();
    }

    public async Task<CheckPasswordResult> CheckPasswordAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (await passwordHasher.VerifyPasswordAsync(credentials.UserName, credentials.Password).ConfigureAwait(false))
        {
            return CheckPasswordResult.Success();
        }

        return new CheckPasswordResult("Invalid username or password.");
    }

    public async Task HandleUnauthorizedAsync(HttpContext httpContext, string failReason)
    {
        await httpContext.Response.WriteAsync(failReason).ConfigureAwait(false);
    }
}
