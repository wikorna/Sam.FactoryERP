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
/// Consumes <see cref="PrintZplCommand"/> — a lightweight "fire-and-print" command
/// that doesn't require a pre-existing PrintJob row.
/// Creates a PrintJob on-the-fly, dispatches ZPL, and publishes the result.
/// </summary>
public sealed partial class PrintZplCommandConsumer : IConsumer<PrintZplCommand>
{
    private readonly IZplPrinterClient _printerClient;
    private readonly ILabelingDbContext _dbContext;
    private readonly ILogger<PrintZplCommandConsumer> _logger;

    public PrintZplCommandConsumer(
        IZplPrinterClient printerClient,
        ILabelingDbContext dbContext,
        ILogger<PrintZplCommandConsumer> logger)
    {
        _printerClient = printerClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PrintZplCommand> context)
    {
        var msg = context.Message;

        LogProcessing(msg.JobId, msg.PrinterId);

        // Resolve printer from registry
        var printer = await _dbContext.Printers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == msg.PrinterId, context.CancellationToken);

        if (printer is null)
        {
            LogPrinterNotFound(msg.PrinterId);
            // Publish failure result and return (don't retry — permanent)
            await context.Publish(
                new PrintZplResult(msg.JobId, "Failed", $"Printer {msg.PrinterId} not found", DateTime.UtcNow),
                context.CancellationToken);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _printerClient.SendZplAsync(printer, msg.Zpl, context.CancellationToken);
            sw.Stop();

            LogSuccess(msg.JobId, printer.Name, sw.ElapsedMilliseconds);

            await context.Publish(
                new PrintZplResult(msg.JobId, "Completed", string.Empty, DateTime.UtcNow),
                context.CancellationToken);
        }
        catch (PermanentPrinterException ex)
        {
            sw.Stop();
            LogFailedPermanent(msg.JobId, printer.Name, ex);

            await context.Publish(
                new PrintZplResult(msg.JobId, "Failed", ex.Message, DateTime.UtcNow),
                context.CancellationToken);
            // Don't rethrow — permanent
        }
        catch (TransientPrinterException ex)
        {
            sw.Stop();
            LogFailedTransient(msg.JobId, printer.Name, ex);

            // Rethrow to let MassTransit retry
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processing PrintZplCommand: JobId={JobId}, PrinterId={PrinterId}")]
    private partial void LogProcessing(Guid jobId, Guid printerId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Printer {PrinterId} not found in registry for PrintZplCommand")]
    private partial void LogPrinterNotFound(Guid printerId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "PrintZplCommand JobId={JobId} sent to {PrinterName} in {ElapsedMs}ms")]
    private partial void LogSuccess(Guid jobId, string printerName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "PrintZplCommand JobId={JobId} PERMANENT failure on {PrinterName}")]
    private partial void LogFailedPermanent(Guid jobId, string printerName, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "PrintZplCommand JobId={JobId} TRANSIENT failure on {PrinterName} — will retry")]
    private partial void LogFailedTransient(Guid jobId, string printerName, Exception ex);
}
