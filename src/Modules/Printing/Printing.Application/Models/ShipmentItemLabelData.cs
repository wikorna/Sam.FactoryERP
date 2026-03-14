namespace Printing.Application.Models;

/// <summary>
/// Aggregated input to the label rendering pipeline.
/// Built inside the consumer from a <c>PrintShipmentItemCommand</c>.
/// </summary>
public sealed record ShipmentItemLabelData
{
    // ── Identity ──────────────────────────────────────────────────────────
    public required Guid BatchId { get; init; }
    public required Guid ItemId { get; init; }
    public required string BatchNumber { get; init; }
    public required int LineNumber { get; init; }

    // ── Label fields ──────────────────────────────────────────────────────
    public required string CustomerCode { get; init; }
    public required string PartNo { get; init; }
    public required string ProductName { get; init; }
    public required string Description { get; init; }
    public required int Quantity { get; init; }
    public string? PoNumber { get; init; }
    public string? PoItem { get; init; }
    public string? DueDate { get; init; }
    public string? RunNo { get; init; }
    public string? Store { get; init; }
    public string? Remarks { get; init; }

    /// <summary>
    /// Pre-computed QR payload from the source CSV, if available.
    /// When non-null, the QR payload builder should prefer it over
    /// computing a new payload so that existing scanner routes remain stable.
    /// </summary>
    public string? PrecomputedQrPayload { get; init; }

    // ── Print parameters ──────────────────────────────────────────────────
    public required int LabelCopies { get; init; }
    public required Guid PrinterId { get; init; }
    public required Guid LabelTemplateId { get; init; }
    public required string RequestedBy { get; init; }
    public required string IdempotencyKey { get; init; }
    public required Guid CorrelationId { get; init; }
}

