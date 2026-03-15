using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Abstractions.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs every request name, duration, and result status.
/// Uses LoggerMessage.Define for high-performance structured logging.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Action<ILogger, string, Exception?> LogStart =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(LogStart)),
            "Handling {RequestName}");

    private static readonly Action<ILogger, string, long, Exception?> LogEnd =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2, nameof(LogEnd)),
            "Handled {RequestName} in {ElapsedMs}ms");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        LogStart(logger, requestName, null);

        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        LogEnd(logger, requestName, sw.ElapsedMilliseconds, null);
        return response;
    }
}
