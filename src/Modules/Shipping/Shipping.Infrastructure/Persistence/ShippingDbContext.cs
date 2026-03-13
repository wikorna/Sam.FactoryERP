using Shipping.Application.Abstractions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Microsoft.EntityFrameworkCore;

namespace Shipping.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Shipping module.
/// All tables live in the <c>shipping</c> PostgreSQL schema.
/// </summary>
public sealed class ShippingDbContext : DbContext, IShippingDbContext
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

    public DbSet<ShipmentBatch> ShipmentBatches => Set<ShipmentBatch>();
    public DbSet<ShipmentBatchItem> ShipmentBatchItems => Set<ShipmentBatchItem>();
    public DbSet<ShipmentBatchRowError> ShipmentBatchRowErrors => Set<ShipmentBatchRowError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("shipping");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShippingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

