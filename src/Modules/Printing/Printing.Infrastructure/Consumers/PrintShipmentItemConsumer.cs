using FactoryERP.Contracts.Shipping;
using MassTransit;
using Microsoft.Extensions.Logging;
using Printing.Application.Abstractions;
using Printing.Application.Models;
using Shipping.Application.Abstractions;

namespace Printing.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="PrintShipmentItemCommand"/>, orchestrates the full
/// label-printing pipeline, and marks the shipment item as printed.
/// </summary>
/// <remarks>
/// <b>Pipeline:</b>
/// <list type="number">
///   <item>Idempotency check — skip if <c>ShipmentBatchItem.IsPrinted</c> is already true.</item>
///   <item><see cref="IQrPayloadBuilder.Build"/> — canonical QR content from item fields.</item>
///   <item><see cref="ILabelTemplateResolver.ResolveAsync"/> — fetch ZPL template body from DB.</item>
///   <item><see cref="ITemplatePrintStrategySelector.GetStrategy"/> → <see cref="ITemplatePrintStrategy.Render"/>
///         — substitute tokens → <see cref="PrintDocument"/>.</item>
///   <item><see cref="IPrinterProfileResolver.ResolveAsync"/> — fetch printer connection from DB.</item>
///   <item><see cref="ILabelPrinterClient.PrintAsync"/> — stream ZPL to printer.</item>
///   <item>Mark <c>item.MarkPrinted()</c> and <c>repository.SaveChangesAsync()</c>.</item>
/// </list>
/// <b>At-least-once safety:</b> steps 2–5 are idempotent (DB reads / pure renders).
/// Step 6 (physical dispatch) may print a duplicate on redelivery, but the
/// <c>IsPrinted</c> guard in step 1 prevents that for already-persisted records.
/// The ZebraLabelPrinterClient's Polly retry covers transient socket failures within
/// a single delivery.
/// </remarks>
public sealed partial class PrintShipmentItemConsumer(
    IShipmentBatchRepository repository,
    IQrPayloadBuilder qrBuilder,
    ILabelTemplateResolver templateResolver,
    IPrinterProfileResolver printerResolver,
    ILabelPrinterClient printerClient,
    ITemplatePrintStrategySelector strategySelector,
    ILogger<PrintShipmentItemConsumer> logger)
    : IConsumer<PrintShipmentItemCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PrintShipmentItemCommand> context)
    {
        var cmd = context.Message;
        LogConsuming(logger, cmd.ItemId, cmd.BatchNumber, cmd.IdempotencyKey);

        // ── 1. Load batch + locate item ───────────────────────────────────
        var batch = await repository.GetByIdAsync(cmd.BatchId, context.CancellationToken);
        if (batch is null)
        {
            LogBatchNotFound(logger, cmd.BatchId, cmd.ItemId);
            return;
        }

        var item = batch.Items.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (item is null)
        {
            LogItemNotFound(logger, cmd.ItemId, cmd.BatchId);
            return;
        }

        // ── 2. Idempotency guard ──────────────────────────────────────────
        if (item.IsPrinted)
        {
            LogAlreadyPrinted(logger, cmd.ItemId, cmd.IdempotencyKey);
            return;
        }

        // ── 3. Build QR payload ───────────────────────────────────────────
        var labelData = MapToLabelData(cmd, item.QrPayload);
        var qrPayload = qrBuilder.Build(labelData);

        // ── 4. Resolve template ───────────────────────────────────────────
        var templateSpec = await templateResolver.ResolveAsync(
            cmd.LabelTemplateId, context.CancellationToken);

        // ── 5. Render ZPL via versioned strategy ──────────────────────────
        var strategy = strategySelector.GetStrategy(templateSpec.Version);
        var document = strategy.Render(labelData, qrPayload, templateSpec);

        // ── 6. Resolve printer profile ────────────────────────────────────
        var printerProfile = await printerResolver.ResolveAsync(
            cmd.PrinterId, context.CancellationToken);

        // ── 7. Dispatch to physical printer ──────────────────────────────
        var result = await printerClient.PrintAsync(
            document, printerProfile, context.CancellationToken);

        if (!result.IsSuccess)
        {
            // Permanent failure (e.g. printer disabled) — do not retry.
            LogPermanentFailure(logger, cmd.ItemId, printerProfile.Name,
                result.ErrorCode ?? "UNKNOWN", result.ErrorMessage ?? string.Empty);

            await context.Publish(new ShipmentItemPrintFailedEvent
            {
                CorrelationId    = cmd.CorrelationId,
                CausationId      = cmd.CommandId,
                BatchId          = cmd.BatchId,
                BatchNumber      = cmd.BatchNumber,
                ItemId           = cmd.ItemId,
                LineNumber       = cmd.LineNumber,
                PartNo           = cmd.PartNo,
                CustomerCode     = cmd.CustomerCode,
                PrinterId        = cmd.PrinterId,
                ErrorCode        = result.ErrorCode ?? "UNKNOWN",
                ErrorMessage     = result.ErrorMessage ?? string.Empty,
                ReviewedByUserId = cmd.RequestedBy,
                RequestedBy      = cmd.RequestedBy,
            }, context.CancellationToken);

            return;
        }

        // ── 8. Mark item printed + persist ───────────────────────────────
        item.MarkPrinted();
        await repository.SaveChangesAsync(context.CancellationToken);

        // ── 9. Notify: item printed ───────────────────────────────────────
        await context.Publish(new ShipmentItemPrintedEvent
        {
            CorrelationId    = cmd.CorrelationId,
            CausationId      = cmd.CommandId,
            BatchId          = cmd.BatchId,
            BatchNumber      = cmd.BatchNumber,
            ItemId           = cmd.ItemId,
            LineNumber       = cmd.LineNumber,
            PartNo           = cmd.PartNo,
            CustomerCode     = cmd.CustomerCode,
            PrinterId        = cmd.PrinterId,
            PrinterName      = printerProfile.Name,
            PrintedAtUtc     = result.DispatchedAtUtc,
            ReviewedByUserId = cmd.RequestedBy,
            RequestedBy      = cmd.RequestedBy,
        }, context.CancellationToken);

        LogCompleted(logger, cmd.ItemId, cmd.BatchNumber, printerProfile.Name,
            result.DispatchedAtUtc);
    }

    // ── Mapping ───────────────────────────────────────────────────────────

    private static ShipmentItemLabelData MapToLabelData(
        PrintShipmentItemCommand cmd, string? precomputedQr) =>
        new()
        {
            BatchId               = cmd.BatchId,
            ItemId                = cmd.ItemId,
            BatchNumber           = cmd.BatchNumber,
            LineNumber            = cmd.LineNumber,
            CustomerCode          = cmd.CustomerCode,
            PartNo                = cmd.PartNo,
            ProductName           = cmd.ProductName,
            Description           = cmd.Description,
            Quantity              = cmd.Quantity,
            PoNumber              = cmd.PoNumber,
            PoItem                = cmd.PoItem,
            DueDate               = cmd.DueDate,
            LabelCopies           = cmd.LabelCopies,
            PrinterId             = cmd.PrinterId,
            LabelTemplateId       = cmd.LabelTemplateId,
            RequestedBy           = cmd.RequestedBy,
            IdempotencyKey        = cmd.IdempotencyKey,
            CorrelationId         = cmd.CorrelationId,
            PrecomputedQrPayload  = precomputedQr,
        };

    // ── Structured log messages ───────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consuming PrintShipmentItemCommand: ItemId={ItemId}, Batch={BatchNumber}, Key={Key}")]
    private static partial void LogConsuming(
        ILogger l, Guid itemId, string batchNumber, string key);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Batch {BatchId} not found for item {ItemId} — skipping.")]
    private static partial void LogBatchNotFound(ILogger l, Guid batchId, Guid itemId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Item {ItemId} not found in batch {BatchId} — skipping.")]
    private static partial void LogItemNotFound(ILogger l, Guid itemId, Guid batchId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Item {ItemId} already printed (key={Key}) — idempotent skip.")]
    private static partial void LogAlreadyPrinted(ILogger l, Guid itemId, string key);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Permanent print failure for item {ItemId} on printer '{Printer}': [{Code}] {Message}")]
    private static partial void LogPermanentFailure(
        ILogger l, Guid itemId, string printer, string code, string message);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Item {ItemId} printed on '{Printer}' at {PrintedAt} (batch={BatchNumber}).")]
    private static partial void LogCompleted(
        ILogger l, Guid itemId, string batchNumber, string printer, DateTime printedAt);
}

