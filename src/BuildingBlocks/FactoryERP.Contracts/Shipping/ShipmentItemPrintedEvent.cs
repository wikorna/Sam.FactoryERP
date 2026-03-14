using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Shipping;

/// <summary>
/// Published by <c>PrintShipmentItemConsumer</c> (WorkerHost) immediately after a
/// label has been successfully dispatched to the physical printer.
/// </summary>
public sealed record ShipmentItemPrintedEvent : IntegrationEvent
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required Guid ItemId { get; init; }
    public required int LineNumber { get; init; }
    public required string PartNo { get; init; }
    public required string CustomerCode { get; init; }
    public required Guid PrinterId { get; init; }
    public required string PrinterName { get; init; }
    public required DateTime PrintedAtUtc { get; init; }

    /// <summary>
    /// Guid.ToString() of the warehouse reviewer — used to route a direct-to-user push.
    /// </summary>
    public required string ReviewedByUserId { get; init; }
}

