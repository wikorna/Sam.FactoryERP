namespace Shipping.Application.Abstractions;

/// <summary>
/// Parses a Marketing-uploaded CSV file into structured rows with per-row validation.
/// </summary>
public interface IShipmentCsvParser
{
    /// <summary>
    /// Parse the CSV stream and return all rows (valid and invalid).
    /// Invalid rows carry error information; callers decide whether to reject the whole batch.
    /// </summary>
    Task<ShipmentCsvParseResult> ParseAsync(Stream stream, CancellationToken ct = default);
}

/// <summary>Result of parsing a Marketing CSV file.</summary>
public sealed class ShipmentCsvParseResult
{
    /// <summary>Total number of data rows (excluding header).</summary>
    public int TotalRows { get; init; }

    /// <summary>Successfully parsed rows.</summary>
    public IReadOnlyList<ShipmentCsvRow> ValidRows { get; init; } = [];

    /// <summary>Rows that failed parsing or validation.</summary>
    public IReadOnlyList<ShipmentCsvRowError> Errors { get; init; } = [];

    /// <summary>Whether the file has no parse errors.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>A single valid row from the Marketing CSV.</summary>
public sealed class ShipmentCsvRow
{
    /// <summary>1-based row number in the source CSV (data rows only).</summary>
    public int RowNumber { get; init; }

    /// <summary>Customer code / customer identifier.</summary>
    public string CustomerCode { get; init; } = string.Empty;

    /// <summary>Part number / SKU.</summary>
    public string PartNo { get; init; } = string.Empty;

    /// <summary>Product display name.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Full product description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Ordered quantity.</summary>
    public int Quantity { get; init; }

    /// <summary>Purchase Order number.</summary>
    public string? PoNumber { get; init; }

    /// <summary>PO line item reference.</summary>
    public string? PoItem { get; init; }

    /// <summary>Due date as a display string.</summary>
    public string? DueDate { get; init; }

    /// <summary>Production run number.</summary>
    public string? RunNo { get; init; }

    /// <summary>Warehouse store / location code.</summary>
    public string? Store { get; init; }

    /// <summary>Free-text remarks.</summary>
    public string? Remarks { get; init; }

    /// <summary>Number of label copies (defaults to 1).</summary>
    public int LabelCopies { get; init; } = 1;
}

/// <summary>A parse or validation error for a single CSV row.</summary>
public sealed class ShipmentCsvRowError
{
    /// <summary>1-based row number in the source CSV.</summary>
    public int RowNumber { get; init; }

    /// <summary>Machine-readable error code (e.g. "MISSING_PART_NO").</summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>Human-readable error message.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
}

