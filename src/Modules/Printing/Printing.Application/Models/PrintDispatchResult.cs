namespace Printing.Application.Models;

/// <summary>
/// Result of dispatching a <see cref="PrintDocument"/> to a physical printer.
/// </summary>
public sealed record PrintDispatchResult
{
    public required bool IsSuccess { get; init; }
    public required DateTime DispatchedAtUtc { get; init; }

    /// <summary>Short error code, e.g. "PRINTER_DISABLED", "CONNECT_TIMEOUT".</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Full error message for diagnostics.</summary>
    public string? ErrorMessage { get; init; }

    // ── Factories ─────────────────────────────────────────────────────────

    /// <summary>Creates a successful result with the current UTC timestamp.</summary>
    public static PrintDispatchResult Success() =>
        new() { IsSuccess = true, DispatchedAtUtc = DateTime.UtcNow };

    /// <summary>Creates a permanent failure result (should not be retried).</summary>
    public static PrintDispatchResult Failure(string errorCode, string errorMessage) =>
        new()
        {
            IsSuccess        = false,
            DispatchedAtUtc  = DateTime.UtcNow,
            ErrorCode        = errorCode,
            ErrorMessage     = errorMessage,
        };
}

