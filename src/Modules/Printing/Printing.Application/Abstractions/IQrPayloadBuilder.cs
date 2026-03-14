using Printing.Application.Models;

namespace Printing.Application.Abstractions;

/// <summary>
/// Builds the canonical QR code payload string from shipment item data.
/// </summary>
/// <remarks>
/// QR payload composition is a domain concern owned by the Printing module.
/// No other module (API, Shipping domain, UI) should produce QR strings directly.
/// </remarks>
public interface IQrPayloadBuilder
{
    /// <summary>
    /// Returns a <see cref="QrPayloadData"/> whose <c>Payload</c> is ready
    /// to be encoded into a QR matrix.
    /// </summary>
    /// <remarks>
    /// If <see cref="ShipmentItemLabelData.PrecomputedQrPayload"/> is non-null and
    /// non-empty the builder SHOULD prefer it over recomputing, so that existing
    /// scanner routes remain stable.
    /// </remarks>
    QrPayloadData Build(ShipmentItemLabelData data);
}

