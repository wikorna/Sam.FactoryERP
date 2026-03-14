namespace Shipping.Application.Features.UploadBatch;

/// <summary>Result returned after uploading and staging a shipment batch CSV.</summary>
public sealed record UploadShipmentBatchResult(
    Guid BatchId,
    string BatchNumber,
    string Status,
    int TotalRows,
    int ValidItemCount,
    int ErrorCount,
    IReadOnlyList<UploadRowErrorDto> Errors);

/// <summary>A single row-level parse or validation error.</summary>
public sealed record UploadRowErrorDto(
    int RowNumber,
    string ErrorCode,
    string ErrorMessage);

