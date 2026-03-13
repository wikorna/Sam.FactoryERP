using FactoryERP.SharedKernel.SeedWork;
using Shipping.Domain.Enums;
using Shipping.Domain.Events;

namespace Shipping.Domain.Aggregates.ShipmentBatchAggregate;

/// <summary>
/// Aggregate root for a shipment batch created by Marketing from one or more Purchase Orders.
/// Owns the full lifecycle: creation → warehouse review → print request → completion.
/// </summary>
/// <remarks>
/// <para><b>Invariant:</b> Items cannot be added/removed after the batch is submitted.</para>
/// <para><b>Invariant:</b> Warehouse review can only occur on a submitted batch.</para>
/// <para><b>Invariant:</b> Print can only be requested on an approved batch.</para>
/// </remarks>
public sealed class ShipmentBatch : AuditableEntity
{
    private readonly List<ShipmentBatchItem> _items = [];
    private readonly List<ShipmentBatchRowError> _rowErrors = [];

    /// <summary>Human-readable batch number, e.g. "SB-20260313-001".</summary>
    public string BatchNumber { get; private set; } = string.Empty;

    /// <summary>Reference to the source Purchase Order(s), comma-separated if multiple.</summary>
    public string PoReference { get; private set; } = string.Empty;

    /// <summary>Current lifecycle state.</summary>
    public ShipmentBatchStatus Status { get; private set; }

    /// <summary>Warehouse review outcome.</summary>
    public WarehouseReviewDecision ReviewDecision { get; private set; }

    /// <summary>Who reviewed the batch (Warehouse user).</summary>
    public Guid? ReviewedByUserId { get; private set; }

    /// <summary>When the review was performed.</summary>
    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>Reviewer's comment (required on rejection, optional on approval).</summary>
    public string? ReviewComment { get; private set; }

    /// <summary>Original CSV file name uploaded by Marketing.</summary>
    public string? SourceFileName { get; private set; }

    /// <summary>SHA-256 hash of the uploaded file for deduplication / audit.</summary>
    public string? SourceFileSha256 { get; private set; }

    /// <summary>Total number of rows in the source CSV (including skipped/error rows).</summary>
    public int SourceRowCount { get; private set; }

    /// <summary>Foreign key to the label template version used for this batch.</summary>
    public Guid? LabelTemplateId { get; private set; }

    /// <summary>Foreign key to the target printer.</summary>
    public Guid? PrinterId { get; private set; }

    /// <summary>When the print request was published to the queue.</summary>
    public DateTime? PrintRequestedAtUtc { get; private set; }

    /// <summary>When all items were printed.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Line items in this shipment batch.</summary>
    public IReadOnlyCollection<ShipmentBatchItem> Items => _items.AsReadOnly();

    /// <summary>CSV parse / validation errors recorded during upload.</summary>
    public IReadOnlyCollection<ShipmentBatchRowError> RowErrors => _rowErrors.AsReadOnly();

    // ── EF Core ───────────────────────────────────────────────────────────
    private ShipmentBatch() { }

    // ── Factory ───────────────────────────────────────────────────────────

    /// <summary>Creates a new draft shipment batch. Items are added separately via <see cref="AddItem"/>.</summary>
    public static ShipmentBatch CreateDraft(
        string batchNumber,
        string poReference,
        string? sourceFileName,
        string? sourceFileSha256,
        int sourceRowCount,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(poReference);

        return new ShipmentBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = batchNumber,
            PoReference = poReference,
            Status = ShipmentBatchStatus.Draft,
            ReviewDecision = WarehouseReviewDecision.Pending,
            SourceFileName = sourceFileName,
            SourceFileSha256 = sourceFileSha256,
            SourceRowCount = sourceRowCount,
            CreatedBy = createdBy,
        };
    }

    // ── Item management ───────────────────────────────────────────────────

    /// <summary>Adds a line item. Only allowed while the batch is in <see cref="ShipmentBatchStatus.Draft"/>.</summary>
    public ShipmentBatchItem AddItem(
        int lineNumber,
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
        EnsureStatus(ShipmentBatchStatus.Draft, "add items");

        var item = ShipmentBatchItem.Create(
            shipmentBatchId: Id,
            lineNumber: lineNumber,
            partNo: partNo,
            productName: productName,
            description: description,
            quantity: quantity,
            poNumber: poNumber,
            poItem: poItem,
            dueDate: dueDate,
            runNo: runNo,
            store: store,
            qrPayload: qrPayload,
            remarks: remarks);

        _items.Add(item);
        return item;
    }

    /// <summary>Records a CSV parse/validation error for the given row.</summary>
    public void AddRowError(int rowNumber, string errorCode, string errorMessage)
    {
        EnsureStatus(ShipmentBatchStatus.Draft, "add row errors");

        _rowErrors.Add(ShipmentBatchRowError.Create(Id, rowNumber, errorCode, errorMessage));
    }

    // ── State transitions ─────────────────────────────────────────────────

    /// <summary>Marketing submits the batch for warehouse review.</summary>
    public void Submit()
    {
        EnsureStatus(ShipmentBatchStatus.Draft, "submit");

        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot submit an empty shipment batch.");

        Status = ShipmentBatchStatus.Submitted;
        ModifiedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ShipmentBatchSubmitted(Id, BatchNumber, _items.Count));
    }

    /// <summary>Warehouse operator opens the batch for review.</summary>
    public void BeginReview()
    {
        EnsureStatus(ShipmentBatchStatus.Submitted, "begin review");

        Status = ShipmentBatchStatus.UnderReview;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Warehouse approves the batch.</summary>
    public void Approve(Guid reviewerUserId, string? comment = null)
    {
        EnsureStatus(ShipmentBatchStatus.UnderReview, "approve");

        Status = ShipmentBatchStatus.Approved;
        ReviewDecision = WarehouseReviewDecision.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewComment = comment;
        ModifiedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ShipmentBatchApproved(Id, BatchNumber, reviewerUserId));
    }

    /// <summary>Warehouse rejects the batch.</summary>
    public void Reject(Guid reviewerUserId, string reason)
    {
        EnsureStatus(ShipmentBatchStatus.UnderReview, "reject");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = ShipmentBatchStatus.Rejected;
        ReviewDecision = WarehouseReviewDecision.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewComment = reason;
        ModifiedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ShipmentBatchRejected(Id, BatchNumber, reviewerUserId, reason));
    }

    /// <summary>Assigns the printer and template, then marks as ready for print.</summary>
    public void PrepareForPrint(Guid printerId, Guid labelTemplateId)
    {
        EnsureStatus(ShipmentBatchStatus.Approved, "prepare for print");

        PrinterId = printerId;
        LabelTemplateId = labelTemplateId;
        Status = ShipmentBatchStatus.ReadyForPrint;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Marks the batch as print-requested after the message is published to the queue.</summary>
    public void MarkPrintRequested()
    {
        EnsureStatus(ShipmentBatchStatus.ReadyForPrint, "request print");

        Status = ShipmentBatchStatus.PrintRequested;
        PrintRequestedAtUtc = DateTime.UtcNow;
        ModifiedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ShipmentBatchPrintRequested(Id, BatchNumber, _items.Count));
    }

    /// <summary>Marks the batch as completed after all labels are printed.</summary>
    public void MarkCompleted()
    {
        EnsureStatus(ShipmentBatchStatus.PrintRequested, "complete");

        Status = ShipmentBatchStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        ModifiedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ShipmentBatchCompleted(Id, BatchNumber));
    }

    /// <summary>Cancels the batch. Only allowed before completion.</summary>
    public void Cancel()
    {
        if (Status is ShipmentBatchStatus.Completed or ShipmentBatchStatus.Canceled)
            throw new InvalidOperationException($"Cannot cancel a batch in '{Status}' state.");

        Status = ShipmentBatchStatus.Canceled;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Allows a rejected batch to be revised and resubmitted.</summary>
    public void RevertToDraft()
    {
        EnsureStatus(ShipmentBatchStatus.Rejected, "revert to draft");

        Status = ShipmentBatchStatus.Draft;
        ReviewDecision = WarehouseReviewDecision.Pending;
        ReviewedByUserId = null;
        ReviewedAtUtc = null;
        ReviewComment = null;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    // ── Guards ─────────────────────────────────────────────────────────────

    /// <summary>True if the batch is in a terminal state.</summary>
    public bool IsTerminal => Status is ShipmentBatchStatus.Completed or ShipmentBatchStatus.Canceled;

    private void EnsureStatus(ShipmentBatchStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {action} a shipment batch in '{Status}' state. Expected '{expected}'.");
    }
}

