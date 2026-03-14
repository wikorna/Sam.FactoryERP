using MediatR;
using Shipping.Application.Abstractions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;

namespace Shipping.Application.Features.GetBatch;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>Gets a shipment batch by ID, including items and row-error count.</summary>
public sealed record GetShipmentBatchQuery(Guid BatchId) : IRequest<ShipmentBatchDto?>;

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Full batch detail DTO.</summary>
public sealed record ShipmentBatchDto(
    Guid Id,
    string BatchNumber,
    string Status,
    string ReviewDecision,
    string? PoReference,
    string? SourceFileName,
    int SourceRowCount,
    int ItemCount,
    int ErrorCount,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    DateTime? ReviewedAtUtc,
    string? ReviewComment,
    IReadOnlyList<ShipmentBatchItemDto> Items);

/// <summary>Line item summary DTO.</summary>
public sealed record ShipmentBatchItemDto(
    Guid Id,
    int LineNumber,
    string CustomerCode,
    string PartNo,
    string ProductName,
    string Description,
    int Quantity,
    string? PoNumber,
    string? PoItem,
    string? DueDate,
    string? RunNo,
    string? Store,
    string? Remarks,
    int LabelCopies,
    bool IsPrinted,
    string ReviewStatus,
    string? ExclusionReason);

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>Handles <see cref="GetShipmentBatchQuery"/>.</summary>
public sealed class GetShipmentBatchQueryHandler(IShipmentBatchRepository repository)
    : IRequestHandler<GetShipmentBatchQuery, ShipmentBatchDto?>
{
    /// <inheritdoc />
    public async Task<ShipmentBatchDto?> Handle(
        GetShipmentBatchQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await repository.GetByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
            return null;

        return MapToDto(batch);
    }

    private static ShipmentBatchDto MapToDto(ShipmentBatch batch)
    {
        var items = batch.Items
            .OrderBy(i => i.LineNumber)
            .Select(i => new ShipmentBatchItemDto(
                i.Id,
                i.LineNumber,
                i.CustomerCode,
                i.PartNo,
                i.ProductName,
                i.Description,
                i.Quantity,
                i.PoNumber,
                i.PoItem,
                i.DueDate,
                i.RunNo,
                i.Store,
                i.Remarks,
                i.LabelCopies,
                i.IsPrinted,
                i.ReviewStatus.ToString(),
                i.ExclusionReason))
            .ToList();

        return new ShipmentBatchDto(
            batch.Id,
            batch.BatchNumber,
            batch.Status.ToString(),
            batch.ReviewDecision.ToString(),
            batch.PoReference,
            batch.SourceFileName,
            batch.SourceRowCount,
            items.Count,
            batch.RowErrors.Count,
            batch.CreatedAtUtc,
            batch.CreatedBy,
            batch.ReviewedAtUtc,
            batch.ReviewComment,
            items);
    }
}

