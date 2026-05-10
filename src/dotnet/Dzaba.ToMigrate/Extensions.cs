using System.IdentityModel.Tokens.Jwt;

namespace Dzaba.ToMigrate;

public static class Extensions
{
    public static string EncodeToString(this JwtSecurityToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}