using FactoryERP.Contracts.Shipping;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shipping.Application.Abstractions;
using Shipping.Domain.Enums;

namespace Shipping.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="ShipmentApprovedForPrintingEvent"/>, transitions the batch from
/// <c>Approved → ReadyForPrint → PrintRequested</c>, and publishes one
/// <see cref="PrintShipmentItemCommand"/> per approved item.
/// </summary>
/// <remarks>
/// <para><b>Idempotency:</b> If the batch is already beyond <c>Approved</c> (e.g. duplicate delivery),
/// the consumer skips processing without error so MassTransit marks the message as consumed.</para>
/// <para><b>At-least-once safety:</b> Commands are published <em>before</em> <c>SaveChanges</c>.
/// If the host crashes between publishing and persisting, on redelivery the batch is still
/// <c>Approved</c> so the consumer re-runs. The downstream
/// <see cref="PrintShipmentItemCommand"/> consumer uses its own <c>IdempotencyKey</c>
/// (<c>"{BatchId}:{ItemId}"</c>) to detect and skip duplicates.</para>
/// </remarks>
public sealed class ShipmentApprovedForPrintingConsumer(
    IShipmentBatchRepository repository,
    IShipmentPrinterResolver printerResolver,
    IPublishEndpoint publishEndpoint,
    ILogger<ShipmentApprovedForPrintingConsumer> logger)
    : IConsumer<ShipmentApprovedForPrintingEvent>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ShipmentApprovedForPrintingEvent> context)
    {
        var msg = context.Message;
        LogConsuming(logger, msg.BatchId, msg.BatchNumber, msg.ReviewDecision);

        // ── 1. Load batch with items ──────────────────────────────────────
        var batch = await repository.GetByIdAsync(msg.BatchId, context.CancellationToken);

        if (batch is null)
        {
            LogBatchNotFound(logger, msg.BatchId);
            return;
        }

        // ── 2. Idempotency guard ──────────────────────────────────────────
        // Batch may have moved beyond Approved if this event was redelivered.
        if (batch.Status != ShipmentBatchStatus.Approved)
        {
            LogAlreadyProcessed(logger, msg.BatchId, batch.Status);
            return;
        }

        // ── 3. Resolve printer + template ─────────────────────────────────
        var (printerId, labelTemplateId) = await printerResolver.ResolveAsync(context.CancellationToken);

        // ── 4. Transition: Approved → ReadyForPrint → PrintRequested ──────
        batch.PrepareForPrint(printerId, labelTemplateId);
        batch.MarkPrintRequested();

        // ── 5. Filter items by review decision ────────────────────────────
        var isPartial = string.Equals(msg.ReviewDecision, "PartiallyApproved", StringComparison.Ordinal);
        var itemsToPrint = isPartial
            ? batch.Items.Where(i => i.ReviewStatus == ItemReviewStatus.Approved).ToList()
            : batch.Items.ToList();

        LogDispatchingCommands(logger, msg.BatchId, itemsToPrint.Count);

        // ── 6. Publish one command per approved item ───────────────────────
        foreach (var item in itemsToPrint)
        {
            await publishEndpoint.Publish(new PrintShipmentItemCommand
            {
                IdempotencyKey   = $"{batch.Id}:{item.Id}",
                CorrelationId    = msg.BatchId,
                CausationId      = msg.MessageId,
                BatchId          = batch.Id,
                ItemId           = item.Id,
                BatchNumber      = batch.BatchNumber,
                LineNumber       = item.LineNumber,
                CustomerCode     = item.CustomerCode,
                PartNo           = item.PartNo,
                ProductName      = item.ProductName,
                Description      = item.Description,
                Quantity         = item.Quantity,
                PoNumber         = item.PoNumber,
                PoItem           = item.PoItem,
                DueDate          = item.DueDate,
                LabelCopies      = item.LabelCopies,
                PrinterId        = printerId,
                LabelTemplateId  = labelTemplateId,
                RequestedBy      = msg.RequestedBy,
            }, context.CancellationToken);
        }

        // ── 7. Notify: batch has entered the print pipeline ──────────────
        await publishEndpoint.Publish(new ShipmentItemPrintQueuedEvent
        {
            CorrelationId     = msg.BatchId,
            CausationId       = msg.MessageId,
            BatchId           = batch.Id,
            BatchNumber       = batch.BatchNumber,
            ApprovedItemCount = itemsToPrint.Count,
            ReviewedByUserId  = msg.ReviewedByUserId.ToString(),
            RequestedBy       = msg.RequestedBy,
            PoReference       = msg.PoReference,
        }, context.CancellationToken);

        // ── 8. Persist state after publishing (at-least-once safety) ──────
        await repository.SaveChangesAsync(context.CancellationToken);

        LogCompleted(logger, msg.BatchId, msg.BatchNumber, itemsToPrint.Count);
    }

    // ── Structured log messages ───────────────────────────────────────────

    private static void LogConsuming(ILogger logger, Guid batchId, string batchNumber, string reviewDecision) => logger.LogInformation("Consuming ShipmentApprovedForPrintingEvent: BatchId={BatchId}, BatchNumber={BatchNumber}, Decision={ReviewDecision}", batchId, batchNumber, reviewDecision);

    private static void LogBatchNotFound(ILogger logger, Guid batchId) => logger.LogWarning("Shipment batch {BatchId} not found — skipping print dispatch.", batchId);

    private static void LogAlreadyProcessed(ILogger logger, Guid batchId, ShipmentBatchStatus status) => logger.LogInformation("Shipment batch {BatchId} already in status '{Status}' — idempotent skip.", batchId, status);

    private static void LogDispatchingCommands(ILogger logger, Guid batchId, int itemCount) => logger.LogInformation("Dispatching {ItemCount} PrintShipmentItemCommand(s) for batch {BatchId}.", batchId, itemCount);

    private static void LogCompleted(ILogger logger, Guid batchId, string batchNumber, int itemCount) => logger.LogInformation("Print dispatch complete: BatchId={BatchId}, BatchNumber={BatchNumber}, ItemCount={ItemCount}.", batchId, batchNumber, itemCount);
}

