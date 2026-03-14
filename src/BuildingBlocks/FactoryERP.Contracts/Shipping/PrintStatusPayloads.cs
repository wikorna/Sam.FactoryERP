namespace FactoryERP.Contracts.Shipping;

// ══════════════════════════════════════════════════════════════════════════════
// Print-status SignalR payload contracts
//
// These records are serialised as JSON into NotificationMessage.Payload and
// delivered to Angular via INotificationClient.ReceiveNotification().
//
// EventType constants (used as the Angular discriminator):
//   "Printing.Shipment.BatchQueued"   — batch entered print pipeline
//   "Printing.Shipment.ItemPrinted"   — single item printed successfully
//   "Printing.Shipment.ItemFailed"    — single item failed permanently
//
// Matching TypeScript interface (for reference):
//   interface BatchQueuedPayload {
//     batchId: string; batchNumber: string; approvedItemCount: number;
//     reviewedByUserId: string; poReference: string | null;
//     occurredAtUtc: string; // ISO 8601
//   }
//   interface ItemPrintedPayload {
//     batchId: string; batchNumber: string; itemId: string;
//     lineNumber: number; partNo: string; customerCode: string;
//     printerName: string; printedAtUtc: string;
//   }
//   interface ItemPrintFailedPayload {
//     batchId: string; batchNumber: string; itemId: string;
//     lineNumber: number; partNo: string; customerCode: string;
//     errorCode: string; errorMessage: string;
//     occurredAtUtc: string;
//   }
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Discriminator strings sent as <c>NotificationMessage.EventType</c>.
/// Angular components switch on these to update their state.
/// </summary>
public static class PrintStatusEventTypes
{
    public const string BatchQueued = "Printing.Shipment.BatchQueued";
    public const string ItemPrinted = "Printing.Shipment.ItemPrinted";
    public const string ItemFailed  = "Printing.Shipment.ItemFailed";
}

// ── Per-event payload shapes ──────────────────────────────────────────────────

/// <summary>Payload for <see cref="PrintStatusEventTypes.BatchQueued"/>.</summary>
public sealed record BatchQueuedPayload
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required int ApprovedItemCount { get; init; }
    public required string ReviewedByUserId { get; init; }
    public string? PoReference { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}

/// <summary>Payload for <see cref="PrintStatusEventTypes.ItemPrinted"/>.</summary>
public sealed record ItemPrintedPayload
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required Guid ItemId { get; init; }
    public required int LineNumber { get; init; }
    public required string PartNo { get; init; }
    public required string CustomerCode { get; init; }
    public required string PrinterName { get; init; }
    public required DateTimeOffset PrintedAtUtc { get; init; }
}

/// <summary>Payload for <see cref="PrintStatusEventTypes.ItemFailed"/>.</summary>
public sealed record ItemPrintFailedPayload
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required Guid ItemId { get; init; }
    public required int LineNumber { get; init; }
    public required string PartNo { get; init; }
    public required string CustomerCode { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}

