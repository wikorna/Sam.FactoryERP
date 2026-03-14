using Printing.Domain;

namespace Printing.Application.Abstractions;

public interface IPrintRequestRepository
{
    Task<PrintRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PrintRequest printRequest, CancellationToken cancellationToken = default);
}

