using FactoryERP.SharedKernel.SeedWork;
using Shipping.Domain.Enums;

namespace Shipping.Domain.Aggregates.ShipmentBatchAggregate;

/// <summary>
/// A single line item within a <see cref="ShipmentBatch"/>.
/// Represents one product row from the source CSV / Purchase Order.
/// </summary>
/// <remarks>
/// Owned by <see cref="ShipmentBatch"/> — never queried independently.
/// All data needed for QR label generation is stored here so the printing
/// pipeline does not need to reach back into the Purchasing module.
/// </remarks>
public sealed class ShipmentBatchItem : BaseEntity
{
    /// <summary>FK to parent <see cref="ShipmentBatch"/>.</summary>
    public Guid ShipmentBatchId { get; private set; }

    /// <summary>1-based line number from the source CSV.</summary>
    public int LineNumber { get; private set; }

    /// <summary>Customer code / customer identifier from the source CSV.</summary>
    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>Part number / SKU.</summary>
    public string PartNo { get; private set; } = string.Empty;

    /// <summary>Product display name.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Full product description for the label.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Ordered quantity.</summary>
    public int Quantity { get; private set; }

    /// <summary>Purchase Order number.</summary>
    public string? PoNumber { get; private set; }

    /// <summary>PO line item reference.</summary>
    public string? PoItem { get; private set; }

    /// <summary>Due date / delivery date as a display string.</summary>
    public string? DueDate { get; private set; }

    /// <summary>Production run number.</summary>
    public string? RunNo { get; private set; }

    /// <summary>Warehouse store / location code.</summary>
    public string? Store { get; private set; }

    /// <summary>QR code payload string.</summary>
    public string? QrPayload { get; private set; }

    /// <summary>Free-text remarks.</summary>
    public string? Remarks { get; private set; }

    /// <summary>Number of label copies to print for this item.</summary>
    public int LabelCopies { get; private set; } = 1;

    /// <summary>Whether this item has been printed successfully.</summary>
    public bool IsPrinted { get; private set; }

    /// <summary>Timestamp when the item was printed.</summary>
    public DateTime? PrintedAtUtc { get; private set; }

    /// <summary>Item-level review status set during partial approval.</summary>
    public ItemReviewStatus ReviewStatus { get; private set; }

    /// <summary>Reason the item was excluded (set during partial approval).</summary>
    public string? ExclusionReason { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────
    /// <summary>Parent batch (EF navigation).</summary>
    public ShipmentBatch? ShipmentBatch { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────
    private ShipmentBatchItem() { }

    // ── Factory ───────────────────────────────────────────────────────────
    internal static ShipmentBatchItem Create(
        Guid shipmentBatchId,
        int lineNumber,
        string customerCode,
        string partNo,
        string productName,
        string description,
        int quantity,
        string? poNumber,
        string? poItem,
        string? dueDate,
        string? runNo,
        string? store,
        string? qrPayload,
        string? remarks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(partNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new ShipmentBatchItem
        {
            Id = Guid.NewGuid(),
            ShipmentBatchId = shipmentBatchId,
            LineNumber = lineNumber,
            CustomerCode = customerCode,
            PartNo = partNo,
            ProductName = productName,
            Description = description,
            Quantity = quantity,
            PoNumber = poNumber,
            PoItem = poItem,
            DueDate = dueDate,
            RunNo = runNo,
            Store = store,
            QrPayload = qrPayload,
            Remarks = remarks,
        };
    }

    // ── State transitions ─────────────────────────────────────────────────

    /// <summary>Marks this item as printed.</summary>
    public void MarkPrinted()
    {
        if (IsPrinted) return; // idempotent
        IsPrinted = true;
        PrintedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Sets the number of label copies for this item.</summary>
    public void SetLabelCopies(int copies)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(copies);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(copies, 100);
        LabelCopies = copies;
    }

    /// <summary>Marks this item as approved during partial approval.</summary>
    internal void ApproveItem()
    {
        ReviewStatus = ItemReviewStatus.Approved;
        ExclusionReason = null;
    }

    /// <summary>Marks this item as excluded during partial approval.</summary>
    internal void ExcludeItem(string? reason)
    {
        ReviewStatus = ItemReviewStatus.Excluded;
        ExclusionReason = reason;
    }

    /// <summary>Resets item-level review status (used when reverting to draft).</summary>
    internal void ResetReview()
    {
        ReviewStatus = ItemReviewStatus.Pending;
        ExclusionReason = null;
    }
}

