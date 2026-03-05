namespace EDI.Application.Features.Files.GetEdiFilePreview;

public sealed record GetEdiFilePreviewResult(
    Guid StagingId,
    int TotalRows,
    int PageNumber,
    int PageSize,
    IReadOnlyList<EdiStagingRowDto> Rows);

public sealed record EdiStagingRowDto(
    Guid Id,
    int RowIndex,
    string RawLine,
    string ParsedColumnsJson,
    bool IsSelected,
    bool IsValid,
    string? ValidationErrorsJson);
