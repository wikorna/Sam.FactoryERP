namespace Shipping.Domain.Enums;

/// <summary>
/// Lifecycle states of a <see cref="Aggregates.ShipmentBatchAggregate.ShipmentBatch"/>.
/// Transitions are enforced inside the aggregate root.
/// </summary>
/// <remarks>
/// Flow: Draft → Submitted → UnderReview → Approved / Rejected → ReadyForPrint → PrintRequested → Completed
/// Marketing creates (Draft), submits to Warehouse (Submitted → UnderReview),
/// Warehouse approves or rejects, approved batches move through the print pipeline.
/// </remarks>
public enum ShipmentBatchStatus
{
    /// <summary>Batch created by Marketing; still editable.</summary>
    Draft = 0,

    /// <summary>Submitted by Marketing for warehouse review; no longer editable.</summary>
    Submitted = 10,

    /// <summary>Warehouse operator has opened the batch for review.</summary>
    UnderReview = 20,

    /// <summary>Warehouse approved the batch; ready for label generation.</summary>
    Approved = 30,

    /// <summary>Warehouse rejected the batch; Marketing may revise and resubmit.</summary>
    Rejected = 40,

    /// <summary>Labels generated; waiting for operator to trigger print.</summary>
    ReadyForPrint = 50,

    /// <summary>Print request published to queue; awaiting worker processing.</summary>
    PrintRequested = 60,

    /// <summary>All labels printed successfully; terminal state.</summary>
    Completed = 100,

    /// <summary>Batch was canceled before completion; terminal state.</summary>
    Canceled = 999,
}

