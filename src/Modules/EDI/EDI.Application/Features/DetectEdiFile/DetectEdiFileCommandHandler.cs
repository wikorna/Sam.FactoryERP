using EDI.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.DetectEdiFile;

/// <summary>
/// Dispatches the detect command to <see cref="IEdiFileDetector"/> (Infrastructure).
/// Keeps the handler thin: validation is handled by <see cref="DetectEdiFileCommandValidator"/>
/// via the pipeline; detection logic lives in Infrastructure.
/// </summary>
public sealed partial class DetectEdiFileCommandHandler(
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

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "EDI detect: handling {FileName} ({SizeBytes} bytes)")]
    private static partial void LogHandling(ILogger l, string fileName, long sizeBytes);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI detect: success — {FileName} → {FileType} (schema={SchemaKey})")]
    private static partial void LogDetected(ILogger l, string fileName, string fileType, string schemaKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: not detected — {FileName}, errors={ErrorCount}")]
    private static partial void LogNotDetected(ILogger l, string fileName, int errorCount);
}
