namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

public class MessageBrokerSettings
{
    public string Host { get; set; }
    public ushort Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string VirtualHost { get; set; }
}
