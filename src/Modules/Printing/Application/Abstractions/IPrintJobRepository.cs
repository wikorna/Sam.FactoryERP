using Printing.Domain;

namespace Printing.Application.Abstractions;

public interface IPrintJobRepository
{
    Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PrintJob>> GetByStatusAsync(PrintJobStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(PrintJob printJob, CancellationToken cancellationToken = default);
    Task UpdateAsync(PrintJob printJob, CancellationToken cancellationToken = default);
}

