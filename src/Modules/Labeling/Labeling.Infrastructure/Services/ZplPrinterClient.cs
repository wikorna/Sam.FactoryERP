using System.Net.Sockets;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using Labeling.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Labeling.Infrastructure.Services;

/// <summary>
/// Resolves the correct <see cref="IPrinterTransport"/> for a <see cref="Printer"/>
/// and sends ZPL with Polly retry for transient network faults.
/// </summary>
public sealed partial class ZplPrinterClient : IZplPrinterClient
{
    private readonly Dictionary<PrinterProtocol, IPrinterTransport> _transports;
    private readonly ILogger<ZplPrinterClient> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public ZplPrinterClient(
        IEnumerable<IPrinterTransport> transports,
        ILogger<ZplPrinterClient> logger)
    {
        _transports = transports.ToDictionary(t => t.Protocol);
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder().Handle<SocketException>().Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    LogRetryAttempt(args.AttemptNumber, printer: "N/A", args.Outcome.Exception?.Message ?? "unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task SendZplAsync(Printer printer, string zplContent, CancellationToken cancellationToken = default)
    {
        if (!printer.IsEnabled)
            throw new PermanentPrinterException(printer.Id.ToString(),
                $"Printer '{printer.Name}' is disabled.");

        if (!_transports.TryGetValue(printer.Protocol, out var transport))
            throw new PermanentPrinterException(printer.Id.ToString(),
                $"No transport registered for protocol '{printer.Protocol}'.");

        LogSendingZpl(printer.Name, printer.Host, printer.Port);

        try
        {
            await _retryPipeline.ExecuteAsync(async ct =>
            {
                await transport.SendAsync(printer.Host, printer.Port, zplContent, ct);
            }, cancellationToken);
        }
        catch (SocketException ex)
        {
            throw new TransientPrinterException(printer.Id.ToString(),
                $"Network error sending to {printer.Host}:{printer.Port}: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            throw new TransientPrinterException(printer.Id.ToString(),
                $"Timeout connecting to {printer.Host}:{printer.Port}: {ex.Message}", ex);
        }
        catch (OperationCanceledException)
        {
            throw; // let MassTransit handle cancellation
        }
        catch (TransientPrinterException)
        {
            throw;
        }
        catch (PermanentPrinterException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PermanentPrinterException(printer.Id.ToString(),
                $"Unexpected error: {ex.Message}", ex);
        }

        LogZplSent(printer.Name);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Sending ZPL to printer {PrinterName} at {Host}:{Port}")]
    private partial void LogSendingZpl(string printerName, string host, int port);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ZPL sent successfully to printer {PrinterName}")]
    private partial void LogZplSent(string printerName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Retry attempt {AttemptNumber} for printer {Printer} — {ErrorMessage}")]
    private partial void LogRetryAttempt(int attemptNumber, string printer, string errorMessage);
}
