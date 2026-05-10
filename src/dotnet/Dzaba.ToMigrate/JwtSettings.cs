namespace Dzaba.ToMigrate;

public class JwtSettings
{
    public string Authority { get; set; }
    public string Audience { get; set; }
    public bool RequireHttps { get; set; }
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public string IssuerSigningKey { get; set; }
}