namespace Dzaba.HomeSecurity.Auth;

public class AuthSettings
{
    public string Authority { get; set; }
    public string Audience { get; set; }
    public bool RequireHttps { get; set; }
}