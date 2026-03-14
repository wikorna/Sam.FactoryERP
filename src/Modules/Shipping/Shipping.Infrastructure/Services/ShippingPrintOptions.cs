namespace Shipping.Infrastructure.Services;

/// <summary>
/// Configuration options for shipment batch printing.
/// Bound from the <c>ShippingPrint</c> appsettings section.
/// </summary>
public sealed class ShippingPrintOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ShippingPrint";

    /// <summary>Default printer ID to use for shipment QR label printing.</summary>
    public Guid PrinterId { get; init; }

    /// <summary>Default label template ID to use for shipment QR labels.</summary>
    public Guid LabelTemplateId { get; init; }
}

