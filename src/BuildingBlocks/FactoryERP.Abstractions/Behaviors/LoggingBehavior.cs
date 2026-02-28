using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Abstractions.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs every request name, duration, and result status.
/// Uses LoggerMessage source generation for high-performance structured logging.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        LogStart(logger, requestName);

        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        LogEnd(logger, requestName, sw.ElapsedMilliseconds);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogStart(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs}ms")]
    private static partial void LogEnd(ILogger logger, string requestName, long elapsedMs);
}
