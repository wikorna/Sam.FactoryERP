using EDI.Application.Features.DetectEdiFile;

namespace EDI.Application.Abstractions;

/// <summary>
/// Detects the EDI file type and validates its structure from an uploaded stream.
/// Implemented in Infrastructure; consumed by Application handlers.
/// </summary>
public interface IEdiFileDetector
{
    /// <summary>
    /// Inspect <paramref name="fileName"/> and <paramref name="content"/> and return a detection result.
    /// Never throws for "expected" invalid-format cases — those are returned as <see cref="DetectEdiFileResult.Failure"/>.
    /// </summary>
    Task<DetectEdiFileResult> DetectAsync(
        string            fileName,
        Stream            content,
        long              sizeBytes,
        string?           clientId,
        CancellationToken ct);
}

