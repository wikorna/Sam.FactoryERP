using MediatR;

namespace EDI.Application.Features.PreviewEdiFile;

/// <summary>
/// Preview the first N lines of a parsed EDI file, showing header metadata
/// and parsed columns according to the file type config.
/// </summary>
public sealed record PreviewEdiFileQuery(Guid JobId, int PreviewLineCount = 20)
    : IRequest<PreviewEdiFileResponse>;

public sealed record PreviewEdiFileResponse(
    Guid JobId,
    string FileName,
    string? FileTypeCode,
    string? FileTypeDisplayName,
    string JobStatus,
    IReadOnlyList<string> HeaderNames,
    IReadOnlyList<PreviewRowDto> Rows,
    int TotalRowCount);

public sealed record PreviewRowDto(
    int RowIndex,
    string RawLine,
    Dictionary<string, string?> ParsedColumns,
    bool IsValid,
    IReadOnlyList<ValidationErrorDto>? ValidationErrors);

public sealed record ValidationErrorDto(string Column, string Error);

