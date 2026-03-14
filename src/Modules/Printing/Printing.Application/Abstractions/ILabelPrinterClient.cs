using Printing.Application.Models;

namespace Printing.Application.Abstractions;

/// <summary>
/// Transport-agnostic client for dispatching a rendered label to a physical printer.
/// </summary>
/// <remarks>
/// Concrete implementations may target Zebra/ZPL (TCP 9100), network PDF spoolers,
/// cloud print services, or test doubles. The abstraction keeps the consumer
/// independent of the underlying protocol.
/// </remarks>
public interface ILabelPrinterClient
{
    /// <summary>
    /// Dispatches the rendered <paramref name="document"/> to the printer
    /// described by <paramref name="printer"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="PrintDispatchResult"/> indicating success or permanent failure.
    /// Throws for transient failures so MassTransit retry can handle them.
    /// </returns>
    Task<PrintDispatchResult> PrintAsync(
        PrintDocument document,
        PrinterProfile printer,
        CancellationToken ct = default);
}

