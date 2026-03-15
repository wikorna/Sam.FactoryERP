using Printing.Domain;

namespace Printing.Application.Abstractions;

/// <summary>
/// Repository abstraction for the <see cref="PrintRequest"/> aggregate.
/// </summary>
public interface IPrintRequestRepository
{
    /// <summary>Gets a request by ID, including all items.</summary>
    Task<PrintRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets a request by its idempotency key, including all items.</summary>
    Task<PrintRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Adds a new print request to the store.</summary>
    void Add(PrintRequest request);

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
