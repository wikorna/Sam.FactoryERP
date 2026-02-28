using FactoryERP.SharedKernel.SeedWork;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// Item master — the core Inventory entity.
/// Maps to the Fiori "Object Page" pattern.
/// </summary>
public sealed class Item : AuditableEntity
{
    private readonly List<ItemUom> _uoms = [];
    private readonly List<ItemLocation> _locations = [];

    private Item() { } // EF Core

    /// <summary>Human-readable item number (unique business key).</summary>
    public string ItemNumber { get; private set; } = string.Empty;

    /// <summary>Short description shown in list views.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Extended / long text.</summary>
    public string? LongDescription { get; private set; }

    /// <summary>Material group / category code.</summary>
    public string? MaterialGroup { get; private set; }

    /// <summary>Base unit of measure (e.g. EA, KG, M).</summary>
    public string BaseUom { get; private set; } = "EA";

    /// <summary>Item lifecycle status.</summary>
    public ItemStatus Status { get; private set; } = ItemStatus.Active;

    /// <summary>Gross weight in BaseUom.</summary>
    public decimal? GrossWeight { get; private set; }

    /// <summary>Net weight in BaseUom.</summary>
    public decimal? NetWeight { get; private set; }

    /// <summary>Weight unit (KG, LB, etc.)</summary>
    public string? WeightUnit { get; private set; }

    // ── Navigation collections (Object Page sections) ──
    public IReadOnlyCollection<ItemUom> Uoms => _uoms.AsReadOnly();
    public IReadOnlyCollection<ItemLocation> Locations => _locations.AsReadOnly();

    // ── Factory method ──
    public static Item Create(
        string itemNumber,
        string description,
        string baseUom,
        string? materialGroup = null,
        string? longDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUom);

        return new Item
        {
            ItemNumber = itemNumber.Trim().ToUpperInvariant(),
            Description = description.Trim(),
            BaseUom = baseUom.Trim().ToUpperInvariant(),
            MaterialGroup = materialGroup?.Trim(),
            LongDescription = longDescription?.Trim()
        };
    }

    // ── Mutators ──
    public void Update(
        string description,
        string baseUom,
        string? materialGroup,
        string? longDescription,
        decimal? grossWeight,
        decimal? netWeight,
        string? weightUnit)
    {
        Description = description.Trim();
        BaseUom = baseUom.Trim().ToUpperInvariant();
        MaterialGroup = materialGroup?.Trim();
        LongDescription = longDescription?.Trim();
        GrossWeight = grossWeight;
        NetWeight = netWeight;
        WeightUnit = weightUnit?.Trim();
    }

    public void Deactivate()
    {
        if (Status == ItemStatus.Inactive)
            return;
        Status = ItemStatus.Inactive;
    }

    public void Activate()
    {
        Status = ItemStatus.Active;
    }

    // ── Child management ──
    public void AddUom(string uomCode, decimal conversionFactor)
    {
        if (_uoms.Any(u => u.UomCode == uomCode))
            return;
        _uoms.Add(ItemUom.Create(Id, uomCode, conversionFactor));
    }

    public void AddLocation(string plant, string storageLocation, bool isDefault = false)
    {
        if (_locations.Any(l => l.Plant == plant && l.StorageLocation == storageLocation))
            return;
        _locations.Add(ItemLocation.Create(Id, plant, storageLocation, isDefault));
    }
}
