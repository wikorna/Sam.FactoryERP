using MediatR;

namespace Shipping.Application.Features.WarehouseReview;

/// <summary>Warehouse fully approves a shipment batch for printing.</summary>
public sealed record ApproveShipmentBatchCommand(
    Guid BatchId,
    Guid ReviewerUserId,
    string? Comment) : IRequest<ReviewResultDto>;

/// <summary>Warehouse rejects a shipment batch back to Marketing.</summary>
public sealed record RejectShipmentBatchCommand(
    Guid BatchId,
    Guid ReviewerUserId,
    string Reason) : IRequest<ReviewResultDto>;

/// <summary>Warehouse partially approves a batch — some items approved, others excluded.</summary>
public sealed record PartiallyApproveShipmentBatchCommand(
    Guid BatchId,
    Guid ReviewerUserId,
    IReadOnlyList<ItemReviewDecisionDto> ItemDecisions,
    string? Comment) : IRequest<PartialApproveResultDto>;

/// <summary>Per-item approval decision provided by the warehouse reviewer.</summary>
public sealed record ItemReviewDecisionDto(
    Guid ItemId,
    bool IsApproved,
    string? ExclusionReason);

