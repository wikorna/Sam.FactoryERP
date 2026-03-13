using Labeling.Domain.Entities;

namespace Labeling.Application.Interfaces;

/// <summary>
/// Low-level transport abstraction for sending ZPL data to a printer.
/// Each protocol (Raw9100, LPR) has its own implementation.
/// </summary>
public interface IPrinterTransport
{
    /// <summary>The protocol this transport handles.</summary>
    PrinterProtocol Protocol { get; }

    /// <summary>
    /// Sends raw ZPL bytes to the specified host/port.
    /// </summary>
    Task SendAsync(string host, int port, string zplContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw binary data to the specified host/port.
    /// </summary>
    Task SendRawAsync(string host, int port, byte[] data, CancellationToken cancellationToken = default);
}
