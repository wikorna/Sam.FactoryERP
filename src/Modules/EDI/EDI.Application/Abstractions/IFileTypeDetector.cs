using EDI.Domain.Entities;
using EDI.Domain.ValueObjects;

namespace EDI.Application.Abstractions;

/// <summary>
/// Detects the file type from a filename using config-driven prefix patterns.
/// </summary>
public interface IFileTypeDetector
{
    /// <summary>
    /// Auto-detect the file type configuration matching the given filename.
    /// Returns null if no matching config is found.
    /// </summary>
    Task<EdiFileTypeConfig?> DetectAsync(string fileName, CancellationToken ct);

    /// <summary>
    /// Detect the file type and return a <see cref="DetectionResult"/> with confidence score.
    /// Never returns null — returns <see cref="DetectionResult.Unknown"/> when nothing matches.
    /// </summary>
    Task<DetectionResult> DetectWithConfidenceAsync(string fileName, CancellationToken ct);

    /// <summary>
    /// Returns all active file type configurations.
    /// </summary>
    Task<IReadOnlyList<EdiFileTypeConfig>> GetAllActiveConfigsAsync(CancellationToken ct);
}

