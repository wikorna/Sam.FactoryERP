namespace Labeling.Domain.Entities;

/// <summary>
/// PrintJob aggregate root — manages the full lifecycle of a ZPL print request.
/// All state transitions are enforced through explicit methods.
/// </summary>
public class PrintJob
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Caller-supplied idempotency key. A unique constraint guarantees
    /// that duplicate requests return the existing job instead of creating a new one.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Foreign key to <see cref="Printer"/> registry.</summary>
    public Guid PrinterId { get; private set; }

    /// <summary>Raw ZPL payload to send to the printer.</summary>
    public string ZplPayload { get; private set; } = string.Empty;

    /// <summary>Number of label copies.</summary>
    public int Copies { get; private set; } = 1;

    /// <summary>Current lifecycle state.</summary>
    public PrintJobStatus Status { get; private set; }

    /// <summary>How many delivery attempts have been made.</summary>
    public int FailCount { get; private set; }

    /// <summary>Classification of the last error (null when no error).</summary>
    public string? LastErrorCode { get; private set; }

    /// <summary>Human-readable last error message.</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>End-to-end correlation identifier.</summary>
    public Guid CorrelationId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Set when the job reaches <see cref="PrintJobStatus.Printed"/>.</summary>
    public DateTime? PrintedAtUtc { get; private set; }

    /// <summary>Who or what requested the print.</summary>
    public string RequestedBy { get; private set; } = string.Empty;

    // ── Navigation (optional, EF only) ────────────────────────────────────
    public Printer? Printer { get; private set; }

    // ── EF Core parameterless ctor ────────────────────────────────────────
    private PrintJob() { }

    // ── Factory ───────────────────────────────────────────────────────────
    public static PrintJob Create(
        string idempotencyKey,
        Guid printerId,
        string zplPayload,
        int copies,
        Guid correlationId,
        string requestedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(zplPayload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(copies);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(copies, 100);

        var now = DateTime.UtcNow;
        return new PrintJob
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            PrinterId = printerId,
            ZplPayload = zplPayload,
            Copies = copies,
            Status = PrintJobStatus.Queued,
            CorrelationId = correlationId,
            RequestedBy = requestedBy,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    // ── State transitions ─────────────────────────────────────────────────

    /// <summary>Consumer picked the job — mark as dispatching.</summary>
    public void MarkDispatching()
    {
        EnsureNotTerminal();
        Status = PrintJobStatus.Dispatching;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>ZPL delivered successfully.</summary>
    public void MarkPrinted()
    {
        if (Status is PrintJobStatus.Printed)
            return; // idempotent

        Status = PrintJobStatus.Printed;
        PrintedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        LastErrorCode = null;
        LastErrorMessage = null;
    }

    /// <summary>Transient failure — MassTransit will retry.</summary>
    public void MarkFailedRetrying(string errorCode, string errorMessage)
    {
        FailCount++;
        LastErrorCode = errorCode;
        LastErrorMessage = Truncate(errorMessage, 2000);
        Status = PrintJobStatus.FailedRetrying;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>All retries exhausted — dead-letter.</summary>
    public void MarkDeadLettered(string errorCode, string errorMessage)
    {
        FailCount++;
        LastErrorCode = errorCode;
        LastErrorMessage = Truncate(errorMessage, 2000);
        Status = PrintJobStatus.DeadLettered;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Operator or system canceled.</summary>
    public void Cancel()
    {
        if (Status is PrintJobStatus.Printed or PrintJobStatus.Canceled)
            return;

        Status = PrintJobStatus.Canceled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    // ── Guards ─────────────────────────────────────────────────────────────

    /// <summary>Returns true if this job has already been successfully printed.</summary>
    public bool IsAlreadyPrinted => Status is PrintJobStatus.Printed;

    /// <summary>Returns true if this job is in a terminal state.</summary>
    public bool IsTerminal => Status is PrintJobStatus.Printed
        or PrintJobStatus.DeadLettered
        or PrintJobStatus.Canceled;

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
            throw new InvalidOperationException(
                $"PrintJob {Id} is in terminal state '{Status}' and cannot transition further.");
    }

    private static string? Truncate(string? value, int maxLength)
        => value?.Length > maxLength ? value[..maxLength] : value;
}
