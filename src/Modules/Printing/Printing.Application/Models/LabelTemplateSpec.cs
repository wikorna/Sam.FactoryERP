namespace Printing.Application.Models;

/// <summary>
/// Resolved label template — the ZPL body + metadata fetched from the database.
/// </summary>
public sealed record LabelTemplateSpec
{
    public required Guid Id { get; init; }

    /// <summary>Canonical key, e.g. "ShipmentQrLabel".</summary>
    public required string TemplateKey { get; init; }

    /// <summary>Semantic version string, e.g. "v1", "v2".</summary>
    public required string Version { get; init; }

    /// <summary>
    /// ZPL body with <c>{{Placeholder}}</c> tokens.
    /// Known tokens: CustomerCode, PartNo, ProductName, Description,
    /// Quantity, PoNumber, PoItem, DueDate, BatchNumber, LineNumber,
    /// QrPayload, LabelCopies, Remarks.
    /// </summary>
    public required string ZplBody { get; init; }

    /// <summary>DPI the template was designed for (203 | 300 | 600).</summary>
    public required int DesignDpi { get; init; }

    public required int LabelWidthMm { get; init; }
    public required int LabelHeightMm { get; init; }
}

