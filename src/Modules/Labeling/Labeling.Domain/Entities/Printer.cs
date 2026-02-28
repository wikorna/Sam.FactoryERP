namespace Labeling.Domain.Entities;

/// <summary>
/// Printer registry entity — replaces hard-coded printer configuration.
/// Each row represents a physical Zebra (or compatible) label printer.
/// </summary>
public class Printer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PrinterProtocol Protocol { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Printer() { } // EF Core

    public static Printer Create(
        string name,
        PrinterProtocol protocol,
        string host,
        int port,
        bool isEnabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        return new Printer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Protocol = protocol,
            Host = host,
            Port = port,
            IsEnabled = isEnabled,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateConnection(string host, int port, PrinterProtocol protocol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        Host = host;
        Port = port;
        Protocol = protocol;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

