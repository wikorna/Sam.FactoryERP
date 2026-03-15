using Printing.Domain.Enums;

namespace Printing.Domain;

/// <summary>
/// Tracks the print state of a single shipment batch item within a <see cref="PrintRequest"/>.
/// Created once per item when a request is dispatched; updated as the job progresses.
/// </summary>
public sealed class PrintRequestItem
{
    public Guid Id { get; private set; }

    public Guid PrintRequestId { get; private set; }
    public PrintRequest? PrintRequest { get; private set; }

    /// <summary>Cross-module reference to the originating shipment batch item (ID only, no nav prop).</summary>
    public Guid ShipmentBatchItemId { get; private set; }

    public int LineNumber { get; private set; }

    public string PartNo { get; private set; } = string.Empty;
    public string CustomerCode { get; private set; } = string.Empty;

    public PrintItemStatus Status { get; private set; }

    /// <summary>Matches <c>PrintDocument.IdempotencyKey</c> — prevents duplicate print dispatches.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Set when a <see cref="PrintJob"/> is dispatched for this item.</summary>
    public Guid? PrintJobId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Required by EF Core
    private PrintRequestItem() { }

    public static PrintRequestItem Create(
        Guid printRequestId,
        Guid shipmentBatchItemId,
        int lineNumber,
        string partNo,
        string customerCode,
        string idempotencyKey)
    {
        var now = DateTime.UtcNow;
        return new PrintRequestItem
        {
            Id                  = Guid.NewGuid(),
            PrintRequestId      = printRequestId,
            ShipmentBatchItemId = shipmentBatchItemId,
            LineNumber          = lineNumber,
            PartNo              = partNo,
            CustomerCode        = customerCode,
            Status              = PrintItemStatus.Pending,
            IdempotencyKey      = idempotencyKey,
            CreatedAtUtc        = now,
            UpdatedAtUtc        = now,
        };
    }

    public void MarkDispatched(Guid printJobId)
    {
        PrintJobId   = printJobId;
        Status       = PrintItemStatus.Dispatched;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPrinted()
    {
        Status       = PrintItemStatus.Printed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status       = PrintItemStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSkipped()
    {
        Status       = PrintItemStatus.Skipped;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
