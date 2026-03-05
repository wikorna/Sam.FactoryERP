using System.Text.RegularExpressions;
using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Entities;
using EDI.Domain.ValueObjects;
using FactoryERP.Abstractions.Caching;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure;

/// <summary>
/// Detects file type by matching filename against prefix patterns stored in DB.
/// Configs are cached via <see cref="ICacheService"/> for performance.
/// </summary>
public sealed partial class FileTypeDetector(
    IEdiFileTypeConfigRepository configRepo,
    ICacheService cache,
    ILogger<FileTypeDetector> logger) : IFileTypeDetector
{
    public async Task<EdiFileTypeConfig?> DetectAsync(string fileName, CancellationToken ct)
    {
        var configs = await GetAllActiveConfigsAsync(ct);

        foreach (var config in configs)
        {
            try
            {
                if (Regex.IsMatch(fileName, config.FilenamePrefixPattern,
                        RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    LogDetected(logger, fileName, config.FileTypeCode);
                    return config;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                LogRegexTimeout(logger, config.FileTypeCode, config.FilenamePrefixPattern);
            }
        }

        LogNoMatch(logger, fileName);
        return null;
    }

    /// <inheritdoc/>
    public async Task<DetectionResult> DetectWithConfidenceAsync(string fileName, CancellationToken ct)
    {
        var configs = await GetAllActiveConfigsAsync(ct);

        foreach (var config in configs)
        {
            try
            {
                if (Regex.IsMatch(fileName, config.FilenamePrefixPattern,
                        RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    var result = DetectionResult.FromFilenamePrefix(config.FileTypeCode);
                    LogDetectedWithConfidence(logger, fileName, config.FileTypeCode, result.ConfidenceScore);
                    return result;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                LogRegexTimeout(logger, config.FileTypeCode, config.FilenamePrefixPattern);
            }
        }

        LogNoMatch(logger, fileName);
        return DetectionResult.Unknown;
    }

    public async Task<IReadOnlyList<EdiFileTypeConfig>> GetAllActiveConfigsAsync(CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            EdiCacheKeys.FileTypeConfigs + ":all",
            async token => await configRepo.GetAllActiveAsync(token),
            EdiCacheKeys.FileTypeConfigSettings(),
            ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI file type detected: {FileName} → {FileTypeCode}")]
    private static partial void LogDetected(ILogger logger, string fileName, string fileTypeCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI file type detected with confidence: {FileName} → {FileTypeCode} (confidence={Confidence:F2})")]
    private static partial void LogDetectedWithConfidence(ILogger logger, string fileName, string fileTypeCode, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "EDI file type regex timeout: {FileTypeCode}, Pattern={Pattern}")]
    private static partial void LogRegexTimeout(ILogger logger, string fileTypeCode, string pattern);

    [LoggerMessage(Level = LogLevel.Warning, Message = "EDI file type not detected: {FileName}")]
    private static partial void LogNoMatch(ILogger logger, string fileName);
}

