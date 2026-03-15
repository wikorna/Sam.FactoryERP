using Printing.Domain;

namespace Printing.Application.Abstractions;

/// <summary>
/// Repository abstraction for the <see cref="PrintJob"/> entity.
/// </summary>
public interface IPrintJobRepository
{
    /// <summary>Gets a job by ID.</summary>
    Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Checks whether a job with the given idempotency key already exists.</summary>
    Task<bool> ExistsByIdempotencyKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Adds a new print job to the store.</summary>
    void Add(PrintJob job);

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
