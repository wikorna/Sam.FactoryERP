namespace Printing.Domain;

/// <summary>
/// Immutable record of the physical print outcome for a <see cref="PrintJob"/>.
/// Multiple results can exist per job — one per dispatch attempt — enabling full reprint history.
/// </summary>
public sealed class PrintResult
{
    public Guid Id { get; private set; }

    public Guid PrintJobId { get; private set; }
    public PrintJob? PrintJob { get; private set; }

    public bool IsSuccess { get; private set; }

    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>Timestamp when the ZPL was successfully accepted by the printer port.</summary>
    public DateTime? DispatchedAtUtc { get; private set; }

    /// <summary>Round-trip duration of the printer socket call in milliseconds.</summary>
    public long? DurationMs { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Required by EF Core
    private PrintResult() { }

    public static PrintResult Success(Guid printJobId, DateTime dispatchedAtUtc, long durationMs) =>
        new()
        {
            Id             = Guid.NewGuid(),
            PrintJobId     = printJobId,
            IsSuccess      = true,
            DispatchedAtUtc = dispatchedAtUtc,
            DurationMs     = durationMs,
            CreatedAtUtc   = DateTime.UtcNow,
        };

    public static PrintResult Failure(Guid printJobId, string? errorCode, string? errorMessage) =>
        new()
        {
            Id           = Guid.NewGuid(),
            PrintJobId   = printJobId,
            IsSuccess    = false,
            ErrorCode    = errorCode,
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTime.UtcNow,
        };
}
