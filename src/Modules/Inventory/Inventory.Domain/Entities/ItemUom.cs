using FactoryERP.SharedKernel.SeedWork;

namespace Inventory.Domain.Entities;

/// <summary>Unit of measure conversion for an Item.</summary>
public sealed class ItemUom : BaseEntity
{
    private ItemUom() { }

    public Guid ItemId { get; private set; }
    public string UomCode { get; private set; } = string.Empty;

    /// <summary>1 BaseUom = ConversionFactor × this UOM.</summary>
    public decimal ConversionFactor { get; private set; }

    internal static ItemUom Create(Guid itemId, string uomCode, decimal conversionFactor) => new()
    {
        ItemId = itemId,
        UomCode = uomCode.Trim().ToUpperInvariant(),
        ConversionFactor = conversionFactor
    };
}
