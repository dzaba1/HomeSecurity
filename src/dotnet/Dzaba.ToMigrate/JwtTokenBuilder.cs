using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Dzaba.ToMigrate;

public sealed class JwtTokenBuilder
{
    private SigningCredentials credentials;
    private string subject;
    private bool subjectSet;
    private string name;
    private bool nameSet;
    private string issuer;
    private string audience;
    private DateTime? expires;
    private DateTime? notBefore;
    private readonly List<Claim> customClaims = new List<Claim>();

    public JwtTokenBuilder WithCredentials(SigningCredentials credentials)
    {
        this.credentials = credentials;
        return this;
    }

    public JwtTokenBuilder WithSecurityKey(string key, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, algorithm);
        return WithCredentials(credentials);
    }

    public JwtTokenBuilder WithSubject(string sub)
    {
        this.subject = sub;
        subjectSet = true;
        return this;
    }

    public JwtTokenBuilder WithName(string name)
    {
        this.name = name;
        nameSet = true;
        return this;
    }

    public JwtTokenBuilder WithIssuer(string issuer)
    {
        this.issuer = issuer;
        return this;
    }

    public JwtTokenBuilder WithAudience(string audience)
    {
        this.audience = audience;
        return this;
    }

    public JwtTokenBuilder WithExpire(DateTime expires)
    {
        this.expires = expires;
        return this;
    }

    public JwtTokenBuilder WithNotBefore(DateTime notBefore)
    {
        this.notBefore = notBefore;
        return this;
    }

    public JwtTokenBuilder WithLifetime(TimeSpan lifetime)
    {
        this.expires = DateTime.Now.Add(lifetime);
        return this;
    }

    public JwtTokenBuilder WithClaim(string type, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var newClaim = new Claim(type, value);
        customClaims.Add(newClaim);
        return this;
    }

    public JwtSecurityToken Build()
    {
        var claims = new List<Claim>(customClaims);

        if (subjectSet)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        }

        if (nameSet)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        return new JwtSecurityToken(issuer, audience, claims, notBefore, expires, credentials);
    }
}