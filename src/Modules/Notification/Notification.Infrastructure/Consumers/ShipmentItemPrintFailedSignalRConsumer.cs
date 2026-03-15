using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Shipping;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// ApiHost-only consumer: receives <see cref="ShipmentItemPrintFailedEvent"/> and pushes
/// a transient <c>"Printing.Shipment.ItemFailed"</c> SignalR status update.
/// </summary>
/// <remarks>
/// Like success events, these are pushed to:
/// <list type="bullet">
///   <item>The warehouse reviewer (direct user push) — they see a red error indicator.</item>
///   <item>The <c>role:Warehouse</c> group — shared dashboards see the error state.</item>
/// </list>
/// Note: The WorkerHost also creates a *persistent* notification for failures via
/// <c>QrPrintFailedNotificationConsumer</c>. This consumer handles the *realtime* UI update.
/// </remarks>
public sealed class ShipmentItemPrintFailedSignalRConsumer(
    INotificationDispatcher dispatcher,
    ILogger<ShipmentItemPrintFailedSignalRConsumer> logger)
    : IConsumer<ShipmentItemPrintFailedEvent>
{
    private const string WarehouseRole = "Warehouse";

    public async Task Consume(ConsumeContext<ShipmentItemPrintFailedEvent> context)
    {
        var msg = context.Message;
        LogConsuming(logger, msg.ItemId, msg.BatchNumber, msg.ErrorCode);

        var payload = new ItemPrintFailedPayload
        {
            BatchId       = msg.BatchId,
            BatchNumber   = msg.BatchNumber,
            ItemId        = msg.ItemId,
            LineNumber    = msg.LineNumber,
            PartNo        = msg.PartNo,
            CustomerCode  = msg.CustomerCode,
            ErrorCode     = msg.ErrorCode,
            ErrorMessage  = msg.ErrorMessage,
            OccurredAtUtc = msg.OccurredAtUtc,
        };

        // ── 1. Direct push to the reviewer ───────────────────────────────
        if (!string.IsNullOrWhiteSpace(msg.ReviewedByUserId))
        {
            await dispatcher.NotifyUserAsync(
                msg.ReviewedByUserId,
                PrintStatusEventTypes.ItemFailed,
                payload,
                context.CancellationToken);
        }

        // ── 2. Role-group push for shared Warehouse dashboards ────────────
        await dispatcher.NotifyRoleAsync(
            WarehouseRole,
            PrintStatusEventTypes.ItemFailed,
            payload,
            context.CancellationToken);

        LogPushed(logger, msg.ItemId, msg.BatchNumber, msg.ErrorCode);
    }

    private static void LogConsuming(ILogger l, Guid itemId, string batchNumber, string errorCode) => l.LogDebug("Consuming ShipmentItemPrintFailedEvent: ItemId={ItemId}, Batch={BatchNumber}, Error={ErrorCode}", itemId, batchNumber, errorCode);

    private static void LogPushed(ILogger l, Guid itemId, string batchNumber, string errorCode) => l.LogInformation("Pushed ItemFailed signal for item {ItemId} in batch {BatchNumber} (code {ErrorCode}).", itemId, batchNumber, errorCode);
}

