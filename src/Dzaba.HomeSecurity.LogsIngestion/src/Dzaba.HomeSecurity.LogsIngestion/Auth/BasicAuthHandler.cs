using Dzaba.BasicAuthentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

internal sealed class BasicAuthHandler : IBasicAuthenticationHandlerService
{
    private readonly IPasswordHasher passwordHasher;
    private readonly AuthDbContext authDbContext;

    public BasicAuthHandler(IPasswordHasher passwordHasher,
        AuthDbContext authDbContext)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(authDbContext);

        this.passwordHasher = passwordHasher;
        this.authDbContext = authDbContext;
    }

    public async Task AddClaimsAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext, ICollection<Claim> claims, object context)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var user = (User)context;
        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    }

    public async Task<CheckPasswordResult> CheckPasswordAsync(BasicAuthenticationCredentials credentials, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var user = await authDbContext.Users.FirstOrDefaultAsync(u => u.Name == credentials.UserName)
            .ConfigureAwait(false);

        if (user != null && passwordHasher.VerifyHashedPassword(user.PasswordHash, credentials.Password))
        {
            return CheckPasswordResult.Success(user);
        }

        return new CheckPasswordResult("Invalid user name or password.");

    }

    public async Task HandleUnauthorizedAsync(HttpContext httpContext, string failReason)
    {
        await httpContext.Response.WriteAsync(failReason).ConfigureAwait(false);
    }
}
