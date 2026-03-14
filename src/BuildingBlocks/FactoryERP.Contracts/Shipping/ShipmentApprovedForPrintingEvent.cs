using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Shipping;

/// <summary>
/// Published when a shipment batch is approved (fully or partially) and is ready
/// for the print pipeline. Consumed by WorkerHost to orchestrate QR label printing.
/// </summary>
/// <remarks>
/// <para>
/// The consumer should use <see cref="IntegrationEvent.MessageId"/> for idempotent processing —
/// if the same MessageId arrives twice (e.g. due to Outbox retry), the consumer must skip
/// the duplicate.
/// </para>
/// <para>
/// <see cref="IntegrationEvent.CorrelationId"/> is set to <see cref="BatchId"/>
/// so all events in this batch's lifecycle share the same correlation.
/// </para>
/// </remarks>
public sealed record ShipmentApprovedForPrintingEvent : IntegrationEvent
{
    /// <summary>The shipment batch aggregate ID.</summary>
    public required Guid BatchId { get; init; }

    /// <summary>Human-readable batch number, e.g. "SB-20260313-001".</summary>
    public required string BatchNumber { get; init; }

    /// <summary>
    /// "Approved" or "PartiallyApproved" — tells the consumer whether
    /// to print all items or only items with <c>ReviewStatus = Approved</c>.
    /// </summary>
    public required string ReviewDecision { get; init; }

    /// <summary>Total items in the batch (before exclusion filtering).</summary>
    public required int TotalItemCount { get; init; }

    /// <summary>Number of items approved for printing.</summary>
    public required int ApprovedItemCount { get; init; }

    /// <summary>Number of items excluded (partial approval only; 0 for full approval).</summary>
    public required int ExcludedItemCount { get; init; }

    /// <summary>Reviewer's user ID.</summary>
    public required Guid ReviewedByUserId { get; init; }

    /// <summary>When the review decision was made.</summary>
    public required DateTime ReviewedAtUtc { get; init; }

    /// <summary>PO reference string for log correlation.</summary>
    public string? PoReference { get; init; }
}

