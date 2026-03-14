using Printing.Application.Models;

namespace Printing.Application.Abstractions;

/// <summary>
/// Resolves a <see cref="PrinterProfile"/> from a printer ID stored in the database.
/// </summary>
public interface IPrinterProfileResolver
{
    /// <summary>
    /// Returns the <see cref="PrinterProfile"/> for the given printer ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no printer is found for the given ID, or the printer is disabled.
    /// </exception>
    Task<PrinterProfile> ResolveAsync(Guid printerId, CancellationToken ct = default);
}

