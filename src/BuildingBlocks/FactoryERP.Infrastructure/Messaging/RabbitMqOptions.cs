namespace FactoryERP.Infrastructure.Messaging;

/// <summary>
/// Strongly-typed options for RabbitMQ connection.
/// Bound from appsettings "RabbitMQ" section.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public bool Enabled { get; set; }
    public string EnvironmentPrefix { get; set; } = "dev";
    public RabbitMqConnectionOptions Connection { get; set; } = new();
}
public sealed class RabbitMqConnectionOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool UseSsl { get; set; }
}

