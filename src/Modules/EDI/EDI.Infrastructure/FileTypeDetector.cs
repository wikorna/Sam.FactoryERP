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
public sealed class FileTypeDetector(
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

    private static void LogDetected(ILogger logger, string fileName, string fileTypeCode) => logger.LogInformation("EDI file type detected: {FileName} → {FileTypeCode}", fileName, fileTypeCode);

    private static void LogDetectedWithConfidence(ILogger logger, string fileName, string fileTypeCode, double confidence) => logger.LogInformation("EDI file type detected with confidence: {FileName} → {FileTypeCode} (confidence={Confidence:F2})", fileName, fileTypeCode, confidence);

    private static void LogRegexTimeout(ILogger logger, string fileTypeCode, string pattern) => logger.LogWarning("EDI file type regex timeout: {FileTypeCode}, Pattern={Pattern}", fileTypeCode, pattern);

    private static void LogNoMatch(ILogger logger, string fileName) => logger.LogWarning("EDI file type not detected: {FileName}", fileName);
}

