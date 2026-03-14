using MediatR;
using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;

namespace Shipping.Application.Features.GetBatchErrors;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>Gets CSV parse/validation errors for a shipment batch.</summary>
public sealed record GetShipmentBatchErrorsQuery(Guid BatchId) : IRequest<ShipmentBatchErrorsResult?>;

// ── Result ────────────────────────────────────────────────────────────────────

/// <summary>Paginated list of row-level errors for a batch.</summary>
public sealed record ShipmentBatchErrorsResult(
    Guid BatchId,
    string BatchNumber,
    int TotalErrors,
    IReadOnlyList<BatchRowErrorDto> Errors);

/// <summary>A single row error DTO.</summary>
public sealed record BatchRowErrorDto(
    Guid Id,
    int RowNumber,
    string ErrorCode,
    string ErrorMessage,
    DateTime CreatedAtUtc);

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>Handles <see cref="GetShipmentBatchErrorsQuery"/>.</summary>
public sealed class GetShipmentBatchErrorsQueryHandler(IShippingDbContext db)
    : IRequestHandler<GetShipmentBatchErrorsQuery, ShipmentBatchErrorsResult?>
{
    /// <inheritdoc />
    public async Task<ShipmentBatchErrorsResult?> Handle(
        GetShipmentBatchErrorsQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await db.ShipmentBatches
            .AsNoTracking()
            .Where(b => b.Id == request.BatchId)
            .Select(b => new { b.Id, b.BatchNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (batch is null)
            return null;

        var errors = await db.ShipmentBatchRowErrors
            .AsNoTracking()
            .Where(e => e.ShipmentBatchId == request.BatchId)
            .OrderBy(e => e.RowNumber)
            .Select(e => new BatchRowErrorDto(
                e.Id,
                e.RowNumber,
                e.ErrorCode,
                e.ErrorMessage,
                e.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new ShipmentBatchErrorsResult(
            batch.Id,
            batch.BatchNumber,
            errors.Count,
            errors);
    }
}

