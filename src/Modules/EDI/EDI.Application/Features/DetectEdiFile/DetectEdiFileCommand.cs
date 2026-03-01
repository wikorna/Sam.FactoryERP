using EDI.Domain.Enums;
using MediatR;

namespace EDI.Application.Features.DetectEdiFile;

/// <summary>
/// Detect EDI file type from an uploaded file stream — stateless, no DB job is created.
/// </summary>
public sealed record DetectEdiFileCommand(
    Stream  Content,
    string  FileName,
    long    SizeBytes,
    string? ClientId = null) : IRequest<DetectEdiFileResult>;

// ── Result ────────────────────────────────────────────────────────────────────

/// <summary>
/// Discriminated result: either a successful detection or a list of validation errors.
/// </summary>
public sealed class DetectEdiFileResult
{
    public bool        Detected  { get; private init; }
    public string      FileName  { get; private init; } = string.Empty;
    public EdiFileType FileType  { get; private init; } = EdiFileType.Unknown;

    /// <summary>Display name of the detected file type, e.g. "SAP Forecast".</summary>
    public string? FileTypeDisplayName { get; private init; }

    // Success fields
    public string?  DocumentNo    { get; private init; }
    public string?  SchemaKey     { get; private init; }
    public string?  SchemaVersion { get; private init; }
    public IReadOnlyDictionary<string, string?>? Header { get; private init; }
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    // Failure fields
    public IReadOnlyList<EdiDetectionError> Errors { get; private init; } = [];

    private DetectEdiFileResult() { }

    public static DetectEdiFileResult Success(
        string                               fileName,
        EdiFileType                          fileType,
        string                               fileTypeDisplayName,
        string                               documentNo,
        string                               schemaKey,
        string                               schemaVersion,
        IReadOnlyDictionary<string, string?> header,
        IReadOnlyList<string>?               warnings = null) =>
        new()
        {
            Detected            = true,
            FileName            = fileName,
            FileType            = fileType,
            FileTypeDisplayName = fileTypeDisplayName,
            DocumentNo          = documentNo,
            SchemaKey           = schemaKey,
            SchemaVersion       = schemaVersion,
            Header              = header,
            Warnings            = warnings ?? [],
        };

    public static DetectEdiFileResult Failure(
        string                          fileName,
        EdiFileType                     fileType,
        IReadOnlyList<EdiDetectionError> errors) =>
        new()
        {
            Detected = false,
            FileName = fileName,
            FileType = fileType,
            Errors   = errors,
        };
}

public sealed record EdiDetectionError(string Code, string Message);

