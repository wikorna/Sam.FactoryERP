using FactoryERP.SharedKernel.SeedWork;

namespace MasterData.Domain.Entities;

/// <summary>
/// Warehouse master record — represents a physical storage location (SAP: Storage Location) within a plant.
/// Warehouse codes are referenced cross-module by Inventory (ItemLocation), Shipping, and Production.
/// All cross-module references store <see cref="Code"/> as a plain string to preserve module boundaries.
/// </summary>
public sealed class Warehouse : BaseEntity
{
    /// <summary>Short code used in cross-module references (e.g. "WH01", "FG01").</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Plant code this warehouse belongs to (e.g. "FACT01").</summary>
    public string Plant { get; private set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; private set; }

    /// <summary>Whether this warehouse is available for use in transactions.</summary>
    public bool IsActive { get; private set; }

    /// <summary>UTC timestamp of creation.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAtUtc { get; private set; }

    private Warehouse() { }

    /// <summary>Creates a new active warehouse.</summary>
    public static Warehouse Create(string code, string name, string plant, string? description)
    {
        return new Warehouse
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Plant = plant.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Updates mutable fields. Code is immutable once created.</summary>
    public void Update(string name, string plant, string? description)
    {
        Name = name.Trim();
        Plant = plant.Trim().ToUpperInvariant();
        Description = description?.Trim();
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Deactivates the warehouse, preventing use in new transactions.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Re-activates a previously deactivated warehouse.</summary>
    public void Activate() => IsActive = true;
}
