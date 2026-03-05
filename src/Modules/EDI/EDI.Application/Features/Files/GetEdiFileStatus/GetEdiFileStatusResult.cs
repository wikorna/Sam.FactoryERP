namespace EDI.Application.Features.Files.GetEdiFileStatus;

public record GetEdiFileStatusResult(
    Guid StagingId,
    string Status,
    int ProgressPercent,
    int? RowCountTotal,
    int? RowCountProcessed,
    string FileName,
    string? ErrorCode,
    string? ErrorMessage,
    int ErrorCount,
    IReadOnlyList<EdiFileErrorSummary> Errors);

public record EdiFileErrorSummary(string Code, string Message, int? RowNumber, string? ColumnName, string Severity);
