using FactoryERP.SharedKernel.SeedWork;

namespace Inventory.Domain.Entities;

/// <summary>Storage location assignment for an Item.</summary>
public sealed class ItemLocation : BaseEntity
{
    private ItemLocation() { }

    public Guid ItemId { get; private set; }
    public string Plant { get; private set; } = string.Empty;
    public string StorageLocation { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    internal static ItemLocation Create(Guid itemId, string plant, string storageLocation, bool isDefault) => new()
    {
        ItemId = itemId,
        Plant = plant.Trim().ToUpperInvariant(),
        StorageLocation = storageLocation.Trim().ToUpperInvariant(),
        IsDefault = isDefault
    };
}
