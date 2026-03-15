using MasterData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application.Interfaces;

/// <summary>MasterData module database abstraction consumed by the Application layer.</summary>
public interface IMasterDataDbContext
{
    /// <summary>Warehouse master records.</summary>
    DbSet<Warehouse> Warehouses { get; }

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
