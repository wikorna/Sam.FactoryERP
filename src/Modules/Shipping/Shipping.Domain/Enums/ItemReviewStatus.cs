namespace Shipping.Domain.Enums;

/// <summary>
/// Per-item review outcome set during warehouse partial approval.
/// Only relevant when the batch-level decision is <see cref="WarehouseReviewDecision.PartiallyApproved"/>.
/// </summary>
public enum ItemReviewStatus
{
    /// <summary>No item-level decision yet (default for full approve/reject).</summary>
    Pending = 0,

    /// <summary>Item approved for printing.</summary>
    Approved = 1,

    /// <summary>Item excluded from this batch by the warehouse reviewer.</summary>
    Excluded = 2,
}

