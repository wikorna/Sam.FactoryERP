using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Shipping;

/// <summary>
/// Published by <c>PrintShipmentItemConsumer</c> (WorkerHost) when a print job fails
/// permanently (printer disabled, or all MassTransit retries exhausted).
/// </summary>
/// <remarks>
/// Permanent failures result in a persistent notification (inbox + toast)
/// to the warehouse reviewer and role "Warehouse".
/// Transient failures (TCP timeout, retry in progress) do NOT publish this event.
/// </remarks>
public sealed record ShipmentItemPrintFailedEvent : IntegrationEvent
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required Guid ItemId { get; init; }
    public required int LineNumber { get; init; }
    public required string PartNo { get; init; }
    public required string CustomerCode { get; init; }
    public required Guid PrinterId { get; init; }

    /// <summary>Short error code, e.g. "PRINTER_DISABLED".</summary>
    public required string ErrorCode { get; init; }

    /// <summary>Full error message for display and diagnostics.</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Guid.ToString() of the warehouse reviewer — used to route a targeted notification.
    /// </summary>
    public required string ReviewedByUserId { get; init; }
}

