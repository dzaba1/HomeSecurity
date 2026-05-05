using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests;

public static class JwtTokenBuilder
{
    public static string CreateToken(string key, string issuer, string audience, string sub, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(ClaimTypes.Name, name)
        ];

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
