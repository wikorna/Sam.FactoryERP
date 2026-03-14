using Printing.Domain;

namespace Printing.Application.Abstractions;

public interface IPrinterRepository
{
    Task<Printer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Printer?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

