namespace FactoryERP.Contracts.Shipping;

/// <summary>
/// Command contract: tells WorkerHost to generate and print a QR label for one shipment item.
/// Published by the ShipmentPrintOrchestrator (future Phase) after resolving printer + template.
/// </summary>
/// <remarks>
/// <para>This is a <b>command</b> (point-to-point), not an event (fan-out).</para>
/// <para>
/// <see cref="IdempotencyKey"/> is derived from <c>{BatchId}:{ItemId}</c> so the
/// consumer can detect and skip duplicate deliveries.
/// </para>
/// </remarks>
public sealed record PrintShipmentItemCommand
{
    /// <summary>Unique command ID (for MassTransit message deduplication).</summary>
    public Guid CommandId { get; init; } = Guid.NewGuid();

    /// <summary>Ties this command back to the shipment batch lifecycle.</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>MessageId of the event that caused this command.</summary>
    public Guid? CausationId { get; init; }

    /// <summary>Deterministic idempotency key: "{BatchId}:{ItemId}".</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Shipment batch ID.</summary>
    public required Guid BatchId { get; init; }

    /// <summary>Shipment batch item ID.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Batch number for logging.</summary>
    public required string BatchNumber { get; init; }

    /// <summary>Line number within the batch.</summary>
    public required int LineNumber { get; init; }

    // ── Label data ─────────────────────────────────────────────────────

    /// <summary>Customer code.</summary>
    public required string CustomerCode { get; init; }

    /// <summary>Part number / SKU.</summary>
    public required string PartNo { get; init; }

    /// <summary>Product display name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Product description.</summary>
    public required string Description { get; init; }

    /// <summary>Quantity.</summary>
    public required int Quantity { get; init; }

    /// <summary>PO number.</summary>
    public string? PoNumber { get; init; }

    /// <summary>PO line item.</summary>
    public string? PoItem { get; init; }

    /// <summary>Due date string.</summary>
    public string? DueDate { get; init; }

    /// <summary>Number of label copies.</summary>
    public required int LabelCopies { get; init; }

    /// <summary>Target printer ID (resolved by orchestrator).</summary>
    public required Guid PrinterId { get; init; }

    /// <summary>Label template ID (resolved by orchestrator).</summary>
    public required Guid LabelTemplateId { get; init; }

    /// <summary>User who approved the batch.</summary>
    public required string RequestedBy { get; init; }

    /// <summary>When this command was created.</summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Schema version for forward-compatible deserialization.</summary>
    public int SchemaVersion { get; init; } = 1;
}

