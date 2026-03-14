using MediatR;

namespace Shipping.Application.Features.UploadBatch;

/// <summary>
/// Upload a Marketing CSV file to create a new shipment batch.
/// The CSV is parsed, validated row-by-row, and staged as a Draft batch.
/// </summary>
public sealed record UploadShipmentBatchCommand(
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    string? PoReference) : IRequest<UploadShipmentBatchResult>;

