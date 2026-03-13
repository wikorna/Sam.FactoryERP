using Shipping.Domain.Aggregates.ShipmentBatchAggregate;

namespace Shipping.Application.Abstractions;

/// <summary>
/// Repository abstraction for the <see cref="ShipmentBatch"/> aggregate.
/// </summary>
public interface IShipmentBatchRepository
{
    /// <summary>Gets a batch by ID, including items and row errors.</summary>
    Task<ShipmentBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets a batch by batch number.</summary>
    Task<ShipmentBatch?> GetByBatchNumberAsync(string batchNumber, CancellationToken ct = default);

    /// <summary>Checks if a batch number already exists.</summary>
    Task<bool> ExistsByBatchNumberAsync(string batchNumber, CancellationToken ct = default);

    /// <summary>Adds a new batch to the store.</summary>
    void Add(ShipmentBatch batch);

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

