using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Shipping;

/// <summary>
/// Published by <c>ShipmentApprovedForPrintingConsumer</c> (WorkerHost) after all
/// <see cref="PrintShipmentItemCommand"/> messages have been dispatched to the queue.
/// Consumers use this as the "batch entered the print pipeline" signal.
/// </summary>
/// <remarks>
/// This is a batch-level event — one per approved batch.
/// Per-item outcomes are reported via <see cref="ShipmentItemPrintedEvent"/>
/// and <see cref="ShipmentItemPrintFailedEvent"/>.
/// </remarks>
public sealed record ShipmentItemPrintQueuedEvent : IntegrationEvent
{
    /// <summary>Shipment batch ID.</summary>
    public required Guid BatchId { get; init; }

    /// <summary>Human-readable batch number, e.g. "SB-20260314-001".</summary>
    public required string BatchNumber { get; init; }

    /// <summary>Number of items queued for printing.</summary>
    public required int ApprovedItemCount { get; init; }

    /// <summary>Who approved the batch (Guid.ToString() of the reviewer).</summary>
    public required string ReviewedByUserId { get; init; }

    /// <summary>PO reference for log correlation.</summary>
    public string? PoReference { get; init; }
}

