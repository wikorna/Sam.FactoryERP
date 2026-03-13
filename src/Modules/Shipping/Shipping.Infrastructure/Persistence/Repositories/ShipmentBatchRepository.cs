using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;

namespace Shipping.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IShipmentBatchRepository"/>.
/// Loads the full aggregate (items + row errors) on every read.
/// </summary>
public sealed class ShipmentBatchRepository : IShipmentBatchRepository
{
    private readonly ShippingDbContext _db;

    public ShipmentBatchRepository(ShippingDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<ShipmentBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ShipmentBatches
            .Include(x => x.Items)
            .Include(x => x.RowErrors)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc />
    public async Task<ShipmentBatch?> GetByBatchNumberAsync(string batchNumber, CancellationToken ct = default)
        => await _db.ShipmentBatches
            .Include(x => x.Items)
            .Include(x => x.RowErrors)
            .FirstOrDefaultAsync(x => x.BatchNumber == batchNumber, ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByBatchNumberAsync(string batchNumber, CancellationToken ct = default)
        => await _db.ShipmentBatches.AnyAsync(x => x.BatchNumber == batchNumber, ct);

    /// <inheritdoc />
    public void Add(ShipmentBatch batch) => _db.ShipmentBatches.Add(batch);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

