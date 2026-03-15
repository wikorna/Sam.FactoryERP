using Printing.Domain.Enums;

namespace Printing.Domain;

/// <summary>
/// Tracks a single label-print dispatch to a physical printer.
/// Created when a <see cref="PrintRequestItem"/> is dispatched by the consumer.
/// </summary>
public sealed class PrintJob
{
    public Guid Id { get; private set; }

    public Guid PrintRequestItemId { get; private set; }
    public PrintRequestItem? PrintRequestItem { get; private set; }

    /// <summary>Cross-module reference to the Labeling module's Printer (ID only, no nav prop).</summary>
    public Guid PrinterId { get; private set; }

    /// <summary>Denormalized printer name for display and audit without cross-module joins.</summary>
    public string PrinterName { get; private set; } = string.Empty;

    /// <summary>Cross-module reference to the Labeling module's LabelTemplate (ID only).</summary>
    public Guid LabelTemplateId { get; private set; }

    /// <summary>Denormalized template version string for audit.</summary>
    public string LabelTemplateVersion { get; private set; } = string.Empty;

    /// <summary>Unique key — prevents duplicate print dispatches for the same command delivery.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public PrintJobStatus Status { get; private set; }

    public int Copies { get; private set; }

    /// <summary>Cumulative count of failed dispatch attempts for this job.</summary>
    public int FailCount { get; private set; }

    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public Guid CorrelationId { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;

    public DateTime QueuedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Required by EF Core
    private PrintJob() { }

    public static PrintJob Create(
        Guid printRequestItemId,
        Guid printerId,
        string printerName,
        Guid labelTemplateId,
        string labelTemplateVersion,
        string idempotencyKey,
        int copies,
        Guid correlationId,
        string requestedBy)
    {
        var now = DateTime.UtcNow;
        return new PrintJob
        {
            Id                   = Guid.NewGuid(),
            PrintRequestItemId   = printRequestItemId,
            PrinterId            = printerId,
            PrinterName          = printerName,
            LabelTemplateId      = labelTemplateId,
            LabelTemplateVersion = labelTemplateVersion,
            IdempotencyKey       = idempotencyKey,
            Status               = PrintJobStatus.Queued,
            Copies               = copies,
            FailCount            = 0,
            CorrelationId        = correlationId,
            RequestedBy          = requestedBy,
            QueuedAtUtc          = now,
            CreatedAtUtc         = now,
            UpdatedAtUtc         = now,
        };
    }

    public void MarkPrinting()
    {
        Status       = PrintJobStatus.Printing;
        StartedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status         = PrintJobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorCode, string? errorMessage)
    {
        Status            = PrintJobStatus.Failed;
        FailCount++;
        LastErrorCode     = errorCode;
        LastErrorMessage  = errorMessage;
        CompletedAtUtc    = DateTime.UtcNow;
        UpdatedAtUtc      = DateTime.UtcNow;
    }
}

