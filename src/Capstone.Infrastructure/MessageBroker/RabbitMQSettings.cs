namespace Capstone.Infrastructure.MessageBroker;

public class RabbitMQSettings
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "capstone.audio";
    public string ExchangeType { get; set; } = "topic";
    public string RoutingKey { get; set; } = "audio.uploaded";
}
