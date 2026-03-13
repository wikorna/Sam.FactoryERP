namespace Shipping.Domain.Aggregates.ShipmentBatchAggregate;

/// <summary>
/// Records a CSV parse or business-validation error for a specific row
/// in the source file. Stored for audit and user feedback.
/// </summary>
public sealed class ShipmentBatchRowError
{
    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private set; }

    /// <summary>FK to parent <see cref="ShipmentBatch"/>.</summary>
    public Guid ShipmentBatchId { get; private set; }

    /// <summary>1-based row number in the source CSV.</summary>
    public int RowNumber { get; private set; }

    /// <summary>Machine-readable error code, e.g. "MISSING_PART_NO", "INVALID_QTY".</summary>
    public string ErrorCode { get; private set; } = string.Empty;

    /// <summary>Human-readable error message.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>When the error was recorded.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────
    /// <summary>Parent batch (EF navigation).</summary>
    public ShipmentBatch? ShipmentBatch { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────
    private ShipmentBatchRowError() { }

    // ── Factory ───────────────────────────────────────────────────────────
    internal static ShipmentBatchRowError Create(
        Guid shipmentBatchId,
        int rowNumber,
        string errorCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ShipmentBatchRowError
        {
            Id = Guid.NewGuid(),
            ShipmentBatchId = shipmentBatchId,
            RowNumber = rowNumber,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}

