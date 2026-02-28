using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Inventory module EF Core DbContext with own schema.</summary>
public sealed class InventoryDbContext : DbContext, IInventoryDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemUom> ItemUoms => Set<ItemUom>();
    public DbSet<ItemLocation> ItemLocations => Set<ItemLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");

        // ── Item ──
        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("Items");
            e.HasKey(x => x.Id);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.ItemNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.Property(x => x.LongDescription).HasMaxLength(2000);
            e.Property(x => x.BaseUom).HasMaxLength(10).IsRequired();
            e.Property(x => x.MaterialGroup).HasMaxLength(40);
            e.Property(x => x.WeightUnit).HasMaxLength(10);
            e.Property(x => x.GrossWeight).HasPrecision(18, 4);
            e.Property(x => x.NetWeight).HasPrecision(18, 4);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            // Unique index on ItemNumber
            e.HasIndex(x => x.ItemNumber).IsUnique();
            // Filtered index for active items
            e.HasIndex(x => x.Status);
            // Search support
            e.HasIndex(x => x.MaterialGroup);

            e.HasMany(x => x.Uoms)
                .WithOne()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Locations)
                .WithOne()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ItemUom ──
        modelBuilder.Entity<ItemUom>(e =>
        {
            e.ToTable("ItemUoms");
            e.HasKey(x => x.Id);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.UomCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.ConversionFactor).HasPrecision(18, 6);
            e.HasIndex(x => new { x.ItemId, x.UomCode }).IsUnique();
        });

        // ── ItemLocation ──
        modelBuilder.Entity<ItemLocation>(e =>
        {
            e.ToTable("ItemLocations");
            e.HasKey(x => x.Id);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.Plant).HasMaxLength(10).IsRequired();
            e.Property(x => x.StorageLocation).HasMaxLength(10).IsRequired();
            e.HasIndex(x => new { x.ItemId, x.Plant, x.StorageLocation }).IsUnique();
        });
    }
}
