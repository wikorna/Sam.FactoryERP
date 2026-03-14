namespace Shipping.Application.Features.WarehouseReview;

/// <summary>Standard result returned after a review action (approve / reject).</summary>
public sealed record ReviewResultDto(
    Guid BatchId,
    string BatchNumber,
    string Status,
    string ReviewDecision,
    Guid ReviewedByUserId,
    DateTime ReviewedAtUtc);

/// <summary>Result returned after partial approval, including per-item breakdown.</summary>
public sealed record PartialApproveResultDto(
    Guid BatchId,
    string BatchNumber,
    string Status,
    string ReviewDecision,
    Guid ReviewedByUserId,
    DateTime ReviewedAtUtc,
    int ApprovedItemCount,
    int ExcludedItemCount,
    IReadOnlyList<ItemReviewResultDto> Items);

/// <summary>Per-item review outcome.</summary>
public sealed record ItemReviewResultDto(
    Guid ItemId,
    int LineNumber,
    string PartNo,
    string ReviewStatus,
    string? ExclusionReason);

