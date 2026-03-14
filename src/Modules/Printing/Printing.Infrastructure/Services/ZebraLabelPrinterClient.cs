using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Services;

/// <summary>
/// Concrete <see cref="ILabelPrinterClient"/> for Zebra label printers using
/// the Raw TCP 9100 protocol (direct ZPL streaming).
/// </summary>
/// <remarks>
/// <para>
/// This is a production-ready stub: it handles connect/write timeouts and applies
/// a Polly exponential retry for transient <see cref="SocketException"/>s and
/// <see cref="TimeoutException"/>s.  Permanent failures (e.g. disabled printer,
/// unresolvable host) are returned as <see cref="PrintDispatchResult.Failure"/>
/// rather than thrown, so MassTransit does not retry them.
/// </para>
/// <para>
/// Future backends (LPR, cloud print, PDF spool) implement <see cref="ILabelPrinterClient"/>
/// independently and are selected by the DI container — no consumer changes required.
/// </para>
/// </remarks>
public sealed partial class ZebraLabelPrinterClient : ILabelPrinterClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WriteTimeout   = TimeSpan.FromSeconds(15);

    private readonly ILogger<ZebraLabelPrinterClient> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public ZebraLabelPrinterClient(ILogger<ZebraLabelPrinterClient> logger)
    {
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                ShouldHandle     = new PredicateBuilder()
                    .Handle<SocketException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    LogRetryAttempt(_logger, args.AttemptNumber,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc />
    public async Task<PrintDispatchResult> PrintAsync(
        PrintDocument document,
        PrinterProfile printer,
        CancellationToken ct = default)
    {
        if (!printer.IsEnabled)
            return PrintDispatchResult.Failure(
                "PRINTER_DISABLED",
                $"Printer '{printer.Name}' is marked disabled — skipping dispatch.");

        LogDispatching(_logger, document.IdempotencyKey, printer.Name, printer.Host, printer.Port,
            document.Copies);

        var zplBytes = Encoding.UTF8.GetBytes(document.ZplContent);

        try
        {
            await _retryPipeline.ExecuteAsync(async innerCt =>
            {
                using var client = new TcpClient();

                // 1. Connect with timeout
                using var connectCts =
                    CancellationTokenSource.CreateLinkedTokenSource(innerCt);
                connectCts.CancelAfter(ConnectTimeout);

                await client.ConnectAsync(printer.Host, printer.Port, connectCts.Token);

                // 2. Write each copy with timeout
                await using var stream = client.GetStream();
                using var writeCts =
                    CancellationTokenSource.CreateLinkedTokenSource(innerCt);
                writeCts.CancelAfter(WriteTimeout);

                for (int copy = 0; copy < document.Copies; copy++)
                {
                    await stream.WriteAsync(zplBytes, writeCts.Token);
                }

                await stream.FlushAsync(writeCts.Token);
            }, ct);

            LogDispatched(_logger, document.IdempotencyKey, printer.Name);
            return PrintDispatchResult.Success();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // connect or write timed out — let MassTransit retry the whole consumer
            throw new TimeoutException(
                $"Timed out connecting or writing to printer '{printer.Name}' " +
                $"at {printer.Host}:{printer.Port}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatching label {IdempotencyKey} to printer '{PrinterName}' ({Host}:{Port}), copies={Copies}")]
    private static partial void LogDispatching(
        ILogger l, string idempotencyKey, string printerName, string host, int port, int copies);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Label {IdempotencyKey} dispatched to printer '{PrinterName}' successfully.")]
    private static partial void LogDispatched(ILogger l, string idempotencyKey, string printerName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Printer connection attempt {Attempt} failed: {Reason}. Retrying…")]
    private static partial void LogRetryAttempt(ILogger l, int attempt, string reason);
}

