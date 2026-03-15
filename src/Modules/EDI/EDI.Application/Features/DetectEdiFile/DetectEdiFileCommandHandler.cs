using EDI.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.DetectEdiFile;

/// <summary>
/// Dispatches the detect command to <see cref="IEdiFileDetector"/> (Infrastructure).
/// Keeps the handler thin: validation is handled by <see cref="DetectEdiFileCommandValidator"/>
/// via the pipeline; detection logic lives in Infrastructure.
/// </summary>
public sealed class DetectEdiFileCommandHandler(
    IEdiFileDetector                     fileDetector,
    ILogger<DetectEdiFileCommandHandler> logger)
    : IRequestHandler<DetectEdiFileCommand, DetectEdiFileResult>
{
    public async Task<DetectEdiFileResult> Handle(
        DetectEdiFileCommand request,
        CancellationToken    cancellationToken)
    {
        LogHandling(logger, request.FileName, request.SizeBytes);

        var result = await fileDetector.DetectAsync(
            request.FileName,
            request.Content,
            request.SizeBytes,
            request.ClientId,
            cancellationToken);

        if (result.Detected)
        {
            var fileTypeName = result.FileType.ToString();
            LogDetected(logger, request.FileName, fileTypeName, result.SchemaKey ?? string.Empty);
        }
        else
            LogNotDetected(logger, request.FileName, result.Errors.Count);

        return result;
    }

    private static readonly Action<ILogger, string, long, Exception?> _logHandling =
        LoggerMessage.Define<string, long>(
            LogLevel.Debug,
            new EventId(2001, nameof(LogHandling)),
            "EDI detect: handling {FileName} ({SizeBytes} bytes)");

    private static readonly Action<ILogger, string, string, string, Exception?> _logDetected =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(2002, nameof(LogDetected)),
            "EDI detect: success — {FileName} → {FileType} (schema={SchemaKey})");

    private static readonly Action<ILogger, string, int, Exception?> _logNotDetected =
        LoggerMessage.Define<string, int>(
            LogLevel.Warning,
            new EventId(2003, nameof(LogNotDetected)),
            "EDI detect: not detected — {FileName}, errors={ErrorCount}");

    private static void LogHandling(ILogger logger, string fileName, long sizeBytes) =>
        _logHandling(logger, fileName, sizeBytes, null);

    private static void LogDetected(ILogger logger, string fileName, string fileType, string schemaKey) =>
        _logDetected(logger, fileName, fileType, schemaKey, null);

    private static void LogNotDetected(ILogger logger, string fileName, int errorCount) =>
        _logNotDetected(logger, fileName, errorCount, null);
}
