namespace Printing.Application.Models;

/// <summary>
/// Resolved printer profile — connection and media details.
/// </summary>
public sealed record PrinterProfile
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    /// <summary>IP address or hostname.</summary>
    public required string Host { get; init; }

    /// <summary>TCP port (default 9100 for Raw ZPL).</summary>
    public required int Port { get; init; }

    /// <summary>Transport protocol, e.g. "Raw9100".</summary>
    public required string Protocol { get; init; }

    public required int Dpi { get; init; }
    public required int LabelWidthMm { get; init; }
    public required int LabelHeightMm { get; init; }
    public required bool IsEnabled { get; init; }
}

