using Printing.Domain.Enums;

namespace Printing.Domain;

/// <summary>
/// Aggregate root that tracks a request to print labels for one shipment batch.
/// One <see cref="PrintRequestItem"/> is created per shipment batch item.
/// </summary>
public sealed class PrintRequest
{
    public Guid Id { get; private set; }

    /// <summary>Cross-module reference to the originating shipment batch (ID only, no nav prop).</summary>
    public Guid BatchId { get; private set; }

    /// <summary>Human-readable batch number, denormalized to avoid cross-module joins in queries.</summary>
    public string BatchNumber { get; private set; } = string.Empty;

    public PrintRequestStatus Status { get; private set; }

    /// <summary>Globally unique key — prevents duplicate print requests for the same batch.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Identity of the user or service that triggered printing.</summary>
    public string RequestedBy { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<PrintRequestItem> Items { get; private set; } = [];

    // Required by EF Core
    private PrintRequest() { }

    public static PrintRequest Create(
        Guid batchId,
        string batchNumber,
        string requestedBy,
        string idempotencyKey)
    {
        var now = DateTime.UtcNow;
        return new PrintRequest
        {
            Id             = Guid.NewGuid(),
            BatchId        = batchId,
            BatchNumber    = batchNumber,
            Status         = PrintRequestStatus.Pending,
            IdempotencyKey = idempotencyKey,
            RequestedBy    = requestedBy,
            CreatedAtUtc   = now,
            UpdatedAtUtc   = now,
        };
    }

    public void MarkProcessing()
    {
        Status       = PrintRequestStatus.Processing;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status       = PrintRequestStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPartiallyCompleted()
    {
        Status       = PrintRequestStatus.PartiallyCompleted;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status       = PrintRequestStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

