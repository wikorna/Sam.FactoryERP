using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Shipping;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// ApiHost-only consumer: receives <see cref="ShipmentItemPrintQueuedEvent"/> and
/// pushes a transient <c>"Printing.Shipment.BatchQueued"</c> SignalR notification
/// to the <c>role:Warehouse</c> group.
/// </summary>
/// <remarks>
/// This is a <b>status-only</b> push — no DB row is written.
/// The Angular print-status panel subscribes to this event type and updates its
/// "queued / in-progress / done" counters in real time.
/// </remarks>
public sealed class ShipmentBatchPrintQueuedSignalRConsumer(
    INotificationDispatcher dispatcher,
    ILogger<ShipmentBatchPrintQueuedSignalRConsumer> logger)
    : IConsumer<ShipmentItemPrintQueuedEvent>
{
    private const string WarehouseRole = "Warehouse";

    public async Task Consume(ConsumeContext<ShipmentItemPrintQueuedEvent> context)
    {
        var msg = context.Message;
        LogConsuming(logger, msg.BatchId, msg.BatchNumber, msg.ApprovedItemCount);

        var payload = new BatchQueuedPayload
        {
            BatchId           = msg.BatchId,
            BatchNumber       = msg.BatchNumber,
            ApprovedItemCount = msg.ApprovedItemCount,
            ReviewedByUserId  = msg.ReviewedByUserId,
            PoReference       = msg.PoReference,
            OccurredAtUtc     = msg.OccurredAtUtc,
        };

        // Push to every connected Warehouse user.
        await dispatcher.NotifyRoleAsync(
            WarehouseRole,
            PrintStatusEventTypes.BatchQueued,
            payload,
            context.CancellationToken);

        LogPushed(logger, msg.BatchId, msg.ApprovedItemCount);
    }

    private static readonly Action<ILogger, Guid, int, Exception?> _logPushed =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Information,
            new EventId(4101, nameof(LogPushed)),
            "Pushed BatchQueued signal for batch {BatchId} ({ItemCount} items) to role:Warehouse.");

    private static void LogConsuming(ILogger l, Guid batchId, string batchNumber, int itemCount) =>
        l.LogDebug("Consuming ShipmentItemPrintQueuedEvent: BatchId={BatchId}, Batch={BatchNumber}, Items={ItemCount}", batchId, batchNumber, itemCount);

    private static void LogPushed(ILogger logger, Guid batchId, int itemCount) =>
        _logPushed(logger, batchId, itemCount, null);
}

