using EDI.Domain.Entities;

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
    /// Returns all active file type configurations.
    /// </summary>
    Task<IReadOnlyList<EdiFileTypeConfig>> GetAllActiveConfigsAsync(CancellationToken ct);
}

