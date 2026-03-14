using MediatR;
using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;
using Shipping.Domain.Enums;

namespace Shipping.Application.Features.WarehouseReview;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>Gets all shipment batches pending warehouse review (Submitted or UnderReview).</summary>
public sealed record GetPendingReviewBatchesQuery : IRequest<IReadOnlyList<PendingReviewBatchDto>>;

// ── DTO ───────────────────────────────────────────────────────────────────────

/// <summary>Summary DTO for a batch awaiting warehouse review.</summary>
public sealed record PendingReviewBatchDto(
    Guid Id,
    string BatchNumber,
    string Status,
    string? PoReference,
    string? SourceFileName,
    int ItemCount,
    DateTime CreatedAtUtc,
    string? CreatedBy);

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>Handles <see cref="GetPendingReviewBatchesQuery"/>.</summary>
public sealed class GetPendingReviewBatchesQueryHandler(IShippingDbContext db)
    : IRequestHandler<GetPendingReviewBatchesQuery, IReadOnlyList<PendingReviewBatchDto>>
{
    private static readonly ShipmentBatchStatus[] ReviewableStatuses =
    [
        ShipmentBatchStatus.Submitted,
        ShipmentBatchStatus.UnderReview,
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingReviewBatchDto>> Handle(
        GetPendingReviewBatchesQuery request,
        CancellationToken cancellationToken)
    {
        return await db.ShipmentBatches
            .AsNoTracking()
            .Where(b => ReviewableStatuses.Contains(b.Status))
            .OrderBy(b => b.CreatedAtUtc)
            .Select(b => new PendingReviewBatchDto(
                b.Id,
                b.BatchNumber,
                b.Status.ToString(),
                b.PoReference,
                b.SourceFileName,
                b.Items.Count,
                b.CreatedAtUtc,
                b.CreatedBy))
            .ToListAsync(cancellationToken);
    }
}

