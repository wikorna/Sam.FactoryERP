namespace Shipping.Domain.Enums;

/// <summary>
/// Outcome of the warehouse review step.
/// Stored on <see cref="Aggregates.ShipmentBatchAggregate.ShipmentBatch"/>
/// when the batch transitions from <see cref="ShipmentBatchStatus.UnderReview"/>.
/// </summary>
public enum WarehouseReviewDecision
{
    /// <summary>No decision has been made yet.</summary>
    Pending = 0,

    /// <summary>Warehouse approved the batch for printing.</summary>
    Approved = 1,

    /// <summary>Warehouse rejected the batch; Marketing must revise.</summary>
    Rejected = 2,
}

