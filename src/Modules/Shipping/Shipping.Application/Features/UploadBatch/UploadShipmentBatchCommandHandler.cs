using System.Globalization;
using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using Shipping.Application.Abstractions;

namespace Shipping.Application.Features.UploadBatch;

/// <summary>
/// Handles CSV upload: parse → validate → create Draft ShipmentBatch → persist.
/// </summary>
public sealed partial class UploadShipmentBatchCommandHandler(
    IShipmentCsvParser csvParser,
    IBatchNumberGenerator batchNumberGenerator,
    IShipmentBatchRepository repository,
    ILogger<UploadShipmentBatchCommandHandler> logger)
    : IRequestHandler<UploadShipmentBatchCommand, UploadShipmentBatchResult>
{
    public async Task<UploadShipmentBatchResult> Handle(
        UploadShipmentBatchCommand request,
        CancellationToken cancellationToken)
    {
        LogUploadStarted(logger, request.FileName, request.FileSizeBytes);

        // 1. Compute SHA-256 of the uploaded file for deduplication / audit.
        var sha256 = await ComputeSha256Async(request.FileStream, cancellationToken);
        request.FileStream.Position = 0; // Reset stream after hashing.

        // 2. Parse the CSV.
        var parseResult = await csvParser.ParseAsync(request.FileStream, cancellationToken);

        LogParsed(logger, request.FileName, parseResult.TotalRows, parseResult.ValidRows.Count, parseResult.Errors.Count);

        // 3. Determine PO reference: use explicit value or derive from CSV rows.
        var poReference = !string.IsNullOrWhiteSpace(request.PoReference)
            ? request.PoReference
            : DerivePoReference(parseResult.ValidRows);

        // 4. Generate batch number.
        var batchNumber = await batchNumberGenerator.GenerateAsync(cancellationToken);

        // 5. Create the Draft aggregate.
        var batch = Domain.Aggregates.ShipmentBatchAggregate.ShipmentBatch.CreateDraft(
            batchNumber: batchNumber,
            poReference: poReference,
            sourceFileName: request.FileName,
            sourceFileSha256: sha256,
            sourceRowCount: parseResult.TotalRows,
            createdBy: "marketing-upload"); // TODO: replace with ICurrentUserService.RequestedBy

        // 6. Add valid items.
        foreach (var row in parseResult.ValidRows)
        {
            var item = batch.AddItem(
                lineNumber: row.RowNumber,
                customerCode: row.CustomerCode,
                partNo: row.PartNo,
                productName: row.ProductName,
                description: row.Description,
                quantity: row.Quantity,
                poNumber: row.PoNumber,
                poItem: row.PoItem,
                dueDate: row.DueDate,
                runNo: row.RunNo,
                store: row.Store,
                qrPayload: null, // QR payload is generated later in the pipeline.
                remarks: row.Remarks);

            if (row.LabelCopies > 1)
            {
                item.SetLabelCopies(row.LabelCopies);
            }
        }

        // 7. Record row-level errors.
        foreach (var err in parseResult.Errors)
        {
            batch.AddRowError(err.RowNumber, err.ErrorCode, err.ErrorMessage);
        }

        // 8. Persist.
        repository.Add(batch);
        await repository.SaveChangesAsync(cancellationToken);

        LogBatchCreated(logger, batchNumber, batch.Id, batch.Items.Count, batch.RowErrors.Count);

        // 9. Map to result DTO.
        var errorDtos = parseResult.Errors
            .Select(e => new UploadRowErrorDto(e.RowNumber, e.ErrorCode, e.ErrorMessage))
            .ToList();

        return new UploadShipmentBatchResult(
            BatchId: batch.Id,
            BatchNumber: batchNumber,
            Status: batch.Status.ToString(),
            TotalRows: parseResult.TotalRows,
            ValidItemCount: batch.Items.Count,
            ErrorCount: batch.RowErrors.Count,
            Errors: errorDtos);
    }

    private static string DerivePoReference(IReadOnlyList<ShipmentCsvRow> rows)
    {
        var poNumbers = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.PoNumber))
            .Select(r => r.PoNumber!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return poNumbers.Count > 0
            ? string.Join(", ", poNumbers)
            : "N/A";
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Shipment batch CSV upload started: {FileName} ({SizeBytes} bytes)")]
    private static partial void LogUploadStarted(ILogger logger, string fileName, long sizeBytes);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CSV parsed: {FileName} → {TotalRows} rows, {ValidCount} valid, {ErrorCount} errors")]
    private static partial void LogParsed(ILogger logger, string fileName, int totalRows, int validCount, int errorCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Shipment batch created: {BatchNumber} (Id={BatchId}), {ItemCount} items, {ErrorCount} row errors")]
    private static partial void LogBatchCreated(ILogger logger, string batchNumber, Guid batchId, int itemCount, int errorCount);
}

