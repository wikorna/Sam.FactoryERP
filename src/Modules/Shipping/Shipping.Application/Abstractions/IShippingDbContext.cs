using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Microsoft.EntityFrameworkCore;

namespace Shipping.Application.Abstractions;

/// <summary>
/// Application-layer abstraction over the Shipping persistence store.
/// Infrastructure provides the concrete <c>ShippingDbContext</c>.
/// </summary>
public interface IShippingDbContext
{
    /// <summary>Shipment batches created by Marketing.</summary>
    DbSet<ShipmentBatch> ShipmentBatches { get; }

    /// <summary>Line items within shipment batches.</summary>
    DbSet<ShipmentBatchItem> ShipmentBatchItems { get; }

    /// <summary>CSV parse / validation errors per batch.</summary>
    DbSet<ShipmentBatchRowError> ShipmentBatchRowErrors { get; }

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

