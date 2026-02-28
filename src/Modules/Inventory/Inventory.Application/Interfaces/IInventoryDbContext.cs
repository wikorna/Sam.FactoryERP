using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Interfaces;

/// <summary>Inventory module database abstraction (Application layer depends on this).</summary>
public interface IInventoryDbContext
{
    DbSet<Item> Items { get; }
    DbSet<ItemUom> ItemUoms { get; }
    DbSet<ItemLocation> ItemLocations { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
