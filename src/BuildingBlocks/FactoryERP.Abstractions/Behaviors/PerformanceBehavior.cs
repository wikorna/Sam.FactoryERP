using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Abstractions.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs a warning when a request exceeds a configurable threshold.
/// Default threshold: 500ms. Configure via appsettings: "Performance:ThresholdMs".
/// </summary>
public sealed partial class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long DefaultThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > DefaultThresholdMs)
        {
            LogSlow(logger, typeof(TRequest).Name, sw.ElapsedMilliseconds, DefaultThresholdMs);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SLOW REQUEST: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)")]
    private static partial void LogSlow(ILogger logger, string requestName, long elapsedMs, long thresholdMs);
}
