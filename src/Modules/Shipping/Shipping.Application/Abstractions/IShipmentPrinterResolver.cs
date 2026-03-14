namespace Shipping.Application.Abstractions;

/// <summary>
/// Resolves the target printer and label template for shipment batch printing.
/// </summary>
/// <remarks>
/// The default implementation reads PrinterId and LabelTemplateId from configuration
/// (<c>ShippingPrint</c> section). Future implementations may query the database
/// or use per-batch routing logic.
/// </remarks>
public interface IShipmentPrinterResolver
{
    /// <summary>
    /// Returns the printer ID and label template ID to use for printing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if neither a printer nor a template can be resolved.
    /// </exception>
    Task<(Guid PrinterId, Guid LabelTemplateId)> ResolveAsync(CancellationToken ct = default);
}

