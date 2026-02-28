using MediatR;

namespace EDI.Application.Features.UploadEdiBatch;

/// <summary>
/// Represents a single file in an upload batch.
/// </summary>
public sealed record UploadFileItem(Stream Content, string FileName, long SizeBytes);

/// <summary>
/// Upload one or more EDI files. System auto-detects file types.
/// </summary>
public sealed record UploadEdiBatchCommand(
    string PartnerCode,
    IReadOnlyList<UploadFileItem> Files) : IRequest<UploadEdiBatchResponse>;

public sealed record UploadEdiBatchResponse(IReadOnlyList<UploadFileResultDto> Results);

public sealed record UploadFileResultDto(
    Guid JobId,
    string FileName,
    string? DetectedFileType,
    string? DetectedDisplayName,
    string Status,
    string? ErrorMessage = null);

