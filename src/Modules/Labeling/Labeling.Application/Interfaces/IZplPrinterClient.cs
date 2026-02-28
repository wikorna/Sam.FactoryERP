using Labeling.Domain.Entities;

namespace Labeling.Application.Interfaces;

/// <summary>
/// High-level printer client that resolves printer from the registry
/// and dispatches ZPL via the appropriate <see cref="IPrinterTransport"/>.
/// Includes connection-level retry/timeout.
/// </summary>
public interface IZplPrinterClient
{
    /// <summary>
    /// Sends ZPL content to the specified printer.
    /// Resolves the printer from the <see cref="Printer"/> registry,
    /// selects the correct transport, and handles retries.
    /// </summary>
    /// <param name="printer">Printer entity from the registry.</param>
    /// <param name="zplContent">Raw ZPL content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendZplAsync(Printer printer, string zplContent, CancellationToken cancellationToken = default);
}
