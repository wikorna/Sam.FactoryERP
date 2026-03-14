using Printing.Domain;

namespace Printing.Application.Abstractions;

public interface IPrinterProfileRepository
{
    Task<PrinterProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

