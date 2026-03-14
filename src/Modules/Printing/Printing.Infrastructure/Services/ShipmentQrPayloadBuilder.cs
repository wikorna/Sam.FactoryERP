using System.Globalization;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Services;

/// <summary>
/// Produces a canonical v1 pipe-delimited QR payload for shipment items.
/// </summary>
/// <remarks>
/// <para><b>Format v1 (pipe-delimited, scanner-routable):</b></para>
/// <code>v1|{CustomerCode}|{PartNo}|{ProductName}|{Quantity}|{PoNumber}|{PoItem}|{DueDate}|{BatchNumber}|{LineNumber}</code>
/// <para>
/// Example:
/// <c>v1|CUST-001|PART-ABC123|Widget Assembly|25|PO-2026-001|10|2026-03-20|SB-20260314-001|1</c>
/// </para>
/// <para>
/// <b>Field mapping:</b>
/// <list type="bullet">
///   <item><c>v1</c> — version prefix; scanner firmware branches on this to pick parser.</item>
///   <item><c>CustomerCode</c> — buyer/customer identifier for receiving.</item>
///   <item><c>PartNo</c> — SKU / part number for inventory lookup.</item>
///   <item><c>ProductName</c> — human-readable; shown on scanner HUD.</item>
///   <item><c>Quantity</c> — unit count on this label.</item>
///   <item><c>PoNumber</c> — Purchase Order number; empty string if null.</item>
///   <item><c>PoItem</c> — PO line item reference; empty string if null.</item>
///   <item><c>DueDate</c> — delivery due date string; empty if null.</item>
///   <item><c>BatchNumber</c> — shipment batch for traceability.</item>
///   <item><c>LineNumber</c> — 1-based row within the batch CSV.</item>
/// </list>
/// </para>
/// <para>
/// Pipe characters inside field values are escaped to <c>\|</c> to preserve
/// split-on-pipe parsing. Backslashes are escaped to <c>\\</c>.
/// </para>
/// </remarks>
public sealed class ShipmentQrPayloadBuilder : IQrPayloadBuilder
{
    private const string PayloadVersion = "v1";

    /// <inheritdoc />
    public QrPayloadData Build(ShipmentItemLabelData data)
    {
        // Prefer pre-computed payload from source CSV — scanner routes stay stable.
        if (!string.IsNullOrWhiteSpace(data.PrecomputedQrPayload))
        {
            return new QrPayloadData
            {
                Payload = data.PrecomputedQrPayload,
                Version = PayloadVersion,
                PartNo  = data.PartNo,
            };
        }

        var payload = string.Join("|",
            PayloadVersion,
            Escape(data.CustomerCode),
            Escape(data.PartNo),
            Escape(data.ProductName),
            data.Quantity.ToString(CultureInfo.InvariantCulture),
            Escape(data.PoNumber),
            Escape(data.PoItem),
            Escape(data.DueDate),
            Escape(data.BatchNumber),
            data.LineNumber.ToString(CultureInfo.InvariantCulture));

        return new QrPayloadData
        {
            Payload = payload,
            Version = PayloadVersion,
            PartNo  = data.PartNo,
        };
    }

    /// <summary>
    /// Escapes backslash and pipe so the scanner's split-on-pipe logic cannot
    /// be confused by data values that contain those characters.
    /// </summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("|", "\\|");
    }
}
