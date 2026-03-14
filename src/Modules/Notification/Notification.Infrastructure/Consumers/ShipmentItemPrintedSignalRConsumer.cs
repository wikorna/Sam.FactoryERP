using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Shipping;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// ApiHost-only consumer: receives <see cref="ShipmentItemPrintedEvent"/> and pushes
/// a transient <c>"Printing.Shipment.ItemPrinted"</c> SignalR status update.
/// </summary>
/// <remarks>
/// Two push targets:
/// <list type="bullet">
///   <item>The warehouse reviewer (direct user push) — they see a per-item tick in the UI.</item>
///   <item>The <c>role:Warehouse</c> group — shared dashboards track overall batch progress.</item>
/// </list>
/// No DB row is written. Missed pushes are acceptable: the reviewer can query batch status
/// via the REST API at any time.
/// </remarks>
public sealed partial class ShipmentItemPrintedSignalRConsumer(
    INotificationDispatcher dispatcher,
    ILogger<ShipmentItemPrintedSignalRConsumer> logger)
    : IConsumer<ShipmentItemPrintedEvent>
{
    private const string WarehouseRole = "Warehouse";

    public async Task Consume(ConsumeContext<ShipmentItemPrintedEvent> context)
    {
        var msg = context.Message;
        LogConsuming(logger, msg.ItemId, msg.BatchNumber, msg.LineNumber);

        var payload = new ItemPrintedPayload
        {
            BatchId       = msg.BatchId,
            BatchNumber   = msg.BatchNumber,
            ItemId        = msg.ItemId,
            LineNumber    = msg.LineNumber,
            PartNo        = msg.PartNo,
            CustomerCode  = msg.CustomerCode,
            PrinterName   = msg.PrinterName,
            PrintedAtUtc  = msg.PrintedAtUtc,
        };

        // ── 1. Direct push to the reviewer ───────────────────────────────
        if (!string.IsNullOrWhiteSpace(msg.ReviewedByUserId))
        {
            await dispatcher.NotifyUserAsync(
                msg.ReviewedByUserId,
                PrintStatusEventTypes.ItemPrinted,
                payload,
                context.CancellationToken);
        }

        // ── 2. Role-group push for shared Warehouse dashboards ────────────
        await dispatcher.NotifyRoleAsync(
            WarehouseRole,
            PrintStatusEventTypes.ItemPrinted,
            payload,
            context.CancellationToken);

        LogPushed(logger, msg.ItemId, msg.BatchNumber, msg.LineNumber);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Consuming ShipmentItemPrintedEvent: ItemId={ItemId}, Batch={BatchNumber}, Line={Line}")]
    private static partial void LogConsuming(
        ILogger l, Guid itemId, string batchNumber, int line);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pushed ItemPrinted signal for item {ItemId} in batch {BatchNumber} (line {Line}).")]
    private static partial void LogPushed(
        ILogger l, Guid itemId, string batchNumber, int line);
}

