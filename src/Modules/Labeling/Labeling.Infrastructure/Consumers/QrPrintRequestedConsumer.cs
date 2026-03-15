using System.Diagnostics;
using FactoryERP.Contracts.Labeling;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using Labeling.Domain.Exceptions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labeling.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer for QR print requests.
/// Loads the PrintJob aggregate, dispatches ZPL via <see cref="IZplPrinterClient"/>,
/// and publishes completion/failure events.
/// </summary>
public sealed class QrPrintRequestedConsumer : IConsumer<QrPrintRequestedIntegrationEvent>
{
    private readonly IZplPrinterClient _printerClient;
    private readonly ILabelingDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<QrPrintRequestedConsumer> _logger;

    public QrPrintRequestedConsumer(
        IZplPrinterClient printerClient,
        ILabelingDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<QrPrintRequestedConsumer> logger)
    {
        _printerClient = printerClient;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<QrPrintRequestedIntegrationEvent> context)
    {
        var message = context.Message;

        LogConsuming(message.PrintJobId, message.PrinterId, message.CorrelationId);

        // 1. Load the PrintJob from DB
        var printJob = await _dbContext.PrintJobs
            .FirstOrDefaultAsync(j => j.Id == message.PrintJobId, context.CancellationToken);

        if (printJob is null)
        {
            LogPrintJobNotFound(message.PrintJobId);
            return;
        }

        // 2. If already Printed → exit (business-level idempotency)
        if (printJob.IsAlreadyPrinted)
        {
            LogAlreadyPrinted(message.PrintJobId);
            return;
        }

        // 3. Load the Printer from the registry
        var printer = await _dbContext.Printers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == message.PrinterId, context.CancellationToken);

        if (printer is null)
        {
            LogPrinterNotFound(message.PrinterId);
            printJob.MarkDeadLettered("PRINTER_NOT_FOUND", $"Printer {message.PrinterId} not found in registry.");
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            return;
        }

        // 4. Mark Dispatching
        printJob.MarkDispatching();
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        var sw = Stopwatch.StartNew();

        try
        {
            // 5. Send ZPL to printer
            await _printerClient.SendZplAsync(printer, printJob.ZplPayload, context.CancellationToken);
            sw.Stop();

            // 6. Success → Mark Printed
            printJob.MarkPrinted();
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            LogPrintSucceeded(message.PrintJobId, printer.Name, sw.ElapsedMilliseconds);

            // Publish completion event
            await _publishEndpoint.Publish(new QrPrintCompletedIntegrationEvent
            {
                CorrelationId = message.CorrelationId,
                RequestedBy = message.RequestedBy,
                PrintJobId = message.PrintJobId,
                PrinterId = message.PrinterId,
                CompletedAtUtc = printJob.PrintedAtUtc!.Value
            }, context.CancellationToken);
        }
        catch (PermanentPrinterException ex)
        {
            sw.Stop();
            // Permanent failure → dead-letter, do NOT rethrow
            printJob.MarkDeadLettered("PERMANENT", ex.Message);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            LogPrintFailedPermanent(message.PrintJobId, printer.Name, ex);

            await _publishEndpoint.Publish(new QrPrintFailedIntegrationEvent
            {
                CorrelationId = message.CorrelationId,
                RequestedBy = message.RequestedBy,
                PrintJobId = message.PrintJobId,
                PrinterId = message.PrinterId,
                FailureReason = ex.Message,
                FailedAtUtc = DateTime.UtcNow
            }, context.CancellationToken);
        }
        catch (TransientPrinterException ex)
        {
            sw.Stop();
            // Transient failure → mark and rethrow for MassTransit retry
            printJob.MarkFailedRetrying("TRANSIENT", ex.Message);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            LogPrintFailedTransient(message.PrintJobId, printer.Name, printJob.FailCount, ex);

            throw; // MassTransit retry policy handles this
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            printJob.MarkFailedRetrying("UNKNOWN", ex.Message);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            LogPrintFailedUnknown(message.PrintJobId, printer.Name, ex);

            throw;
        }
    }

    // ── Structured logging ────────────────────────────────────────────────

    private void LogConsuming(Guid printJobId, Guid printerId, Guid correlationId) => _logger.LogInformation("Consuming QrPrintRequested: PrintJobId={PrintJobId}, PrinterId={PrinterId}, CorrelationId={CorrelationId}", printJobId, printerId, correlationId);

    private void LogPrintJobNotFound(Guid printJobId) => _logger.LogWarning("PrintJob {PrintJobId} not found in database — skipping", printJobId);

    private void LogPrinterNotFound(Guid printerId) => _logger.LogWarning("Printer {PrinterId} not found in registry — dead-lettering", printerId);

    private void LogAlreadyPrinted(Guid printJobId) => _logger.LogInformation("PrintJob {PrintJobId} already printed — skipping duplicate", printJobId);

    private void LogPrintSucceeded(Guid printJobId, string printerName, long elapsedMs) => _logger.LogInformation("PrintJob {PrintJobId} sent to printer {PrinterName} in {ElapsedMs}ms", printJobId, printerName, elapsedMs);

    private void LogPrintFailedPermanent(Guid printJobId, string printerName, Exception ex) => _logger.LogError(ex, "PrintJob {PrintJobId} PERMANENT failure on printer {PrinterName}", printJobId, printerName);

    private void LogPrintFailedTransient(Guid printJobId, string printerName, int failCount, Exception ex) => _logger.LogWarning(ex, "PrintJob {PrintJobId} TRANSIENT failure on printer {PrinterName}, attempt #{FailCount} — will retry", printJobId, printerName, failCount);

    private void LogPrintFailedUnknown(Guid printJobId, string printerName, Exception ex) => _logger.LogError(ex, "PrintJob {PrintJobId} UNKNOWN failure on printer {PrinterName}", printJobId, printerName);
}
