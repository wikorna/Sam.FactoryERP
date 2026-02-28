using MediatR;

namespace EDI.Application.Features.GetFileTypeConfigs;

/// <summary>
/// Returns all active file type configurations for the frontend dropdown/display.
/// </summary>
public sealed record GetFileTypeConfigsQuery : IRequest<GetFileTypeConfigsResponse>;

public sealed record GetFileTypeConfigsResponse(IReadOnlyList<FileTypeConfigDto> Configs);

public sealed record FileTypeConfigDto(
    Guid Id,
    string FileTypeCode,
    string DisplayName,
    string FilenamePrefixPattern,
    string Delimiter,
    bool HasHeaderRow,
    int HeaderLineCount,
    int SkipLines,
    string SchemaVersion,
    long MaxFileSizeBytes,
    IReadOnlyList<ColumnDefinitionDto> Columns);

public sealed record ColumnDefinitionDto(
    int Ordinal,
    string ColumnName,
    string DataType,
    bool IsRequired,
    int? MaxLength,
    string? ValidationRegex,
    string? DisplayLabel);

