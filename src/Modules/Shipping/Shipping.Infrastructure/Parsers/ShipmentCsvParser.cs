using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Shipping.Application.Abstractions;

namespace Shipping.Infrastructure.Parsers;

/// <summary>
/// Parses a Marketing-format CSV into <see cref="ShipmentCsvRow"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Expected CSV columns (order matters, header row required):
/// <c>CustomerCode, PartNo, ProductName, Description, Quantity, PoNumber, PoItem, DueDate, RunNo, Store, Remarks, LabelCopies</c>
/// </para>
/// <para>
/// Rules:
/// <list type="bullet">
///   <item>First row MUST be a header (skipped).</item>
///   <item><c>CustomerCode</c>, <c>PartNo</c>, <c>ProductName</c> are required.</item>
///   <item><c>Quantity</c> must be a positive integer.</item>
///   <item><c>LabelCopies</c> defaults to 1 if missing or invalid.</item>
///   <item>Double-quoted fields are supported.</item>
///   <item>Rows with errors are recorded but not fatal — valid rows still proceed.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ShipmentCsvParser(ILogger<ShipmentCsvParser> logger) : IShipmentCsvParser
{
    // ── Column ordinal constants ──────────────────────────────────────────
    private const int ColCustomerCode = 0;
    private const int ColPartNo = 1;
    private const int ColProductName = 2;
    private const int ColDescription = 3;
    private const int ColQuantity = 4;
    private const int ColPoNumber = 5;
    private const int ColPoItem = 6;
    private const int ColDueDate = 7;
    private const int ColRunNo = 8;
    private const int ColStore = 9;
    private const int ColRemarks = 10;
    private const int ColLabelCopies = 11;
    private const int MinRequiredColumns = 5; // up to Quantity

    /// <inheritdoc />
    public async Task<ShipmentCsvParseResult> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var validRows = new List<ShipmentCsvRow>();
        var errors = new List<ShipmentCsvRowError>();
        int lineNo = 0;
        int dataRow = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            // Skip header row.
            if (lineNo == 1)
            {
                LogHeaderSkipped(logger, line);
                continue;
            }

            // Skip blank lines.
            if (string.IsNullOrWhiteSpace(line))
                continue;

            dataRow++;
            var fields = SplitCsvLine(line, ',');

            // Validate minimum column count.
            if (fields.Count < MinRequiredColumns)
            {
                errors.Add(new ShipmentCsvRowError
                {
                    RowNumber = dataRow,
                    ErrorCode = "INSUFFICIENT_COLUMNS",
                    ErrorMessage = $"Expected at least {MinRequiredColumns} columns, got {fields.Count}.",
                });
                continue;
            }

            // Parse and validate individual fields.
            var rowErrors = new List<ShipmentCsvRowError>();

            var customerCode = GetField(fields, ColCustomerCode);
            if (string.IsNullOrWhiteSpace(customerCode))
            {
                rowErrors.Add(new ShipmentCsvRowError
                {
                    RowNumber = dataRow,
                    ErrorCode = "MISSING_CUSTOMER_CODE",
                    ErrorMessage = "CustomerCode is required.",
                });
            }

            var partNo = GetField(fields, ColPartNo);
            if (string.IsNullOrWhiteSpace(partNo))
            {
                rowErrors.Add(new ShipmentCsvRowError
                {
                    RowNumber = dataRow,
                    ErrorCode = "MISSING_PART_NO",
                    ErrorMessage = "PartNo is required.",
                });
            }

            var productName = GetField(fields, ColProductName);
            if (string.IsNullOrWhiteSpace(productName))
            {
                rowErrors.Add(new ShipmentCsvRowError
                {
                    RowNumber = dataRow,
                    ErrorCode = "MISSING_PRODUCT_NAME",
                    ErrorMessage = "ProductName is required.",
                });
            }

            var quantityStr = GetField(fields, ColQuantity);
            if (!int.TryParse(quantityStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
            {
                rowErrors.Add(new ShipmentCsvRowError
                {
                    RowNumber = dataRow,
                    ErrorCode = "INVALID_QUANTITY",
                    ErrorMessage = $"Quantity must be a positive integer. Got: '{quantityStr}'.",
                });
            }

            // If any required-field errors, skip this row.
            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            // Optional fields.
            var description = GetField(fields, ColDescription) ?? string.Empty;
            var poNumber = GetField(fields, ColPoNumber);
            var poItem = GetField(fields, ColPoItem);
            var dueDate = GetField(fields, ColDueDate);
            var runNo = GetField(fields, ColRunNo);
            var store = GetField(fields, ColStore);
            var remarks = GetField(fields, ColRemarks);

            var labelCopiesStr = GetField(fields, ColLabelCopies);
            var labelCopies = 1;
            if (!string.IsNullOrWhiteSpace(labelCopiesStr))
            {
                if (int.TryParse(labelCopiesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 && parsed <= 100)
                {
                    labelCopies = parsed;
                }
                else
                {
                    errors.Add(new ShipmentCsvRowError
                    {
                        RowNumber = dataRow,
                        ErrorCode = "INVALID_LABEL_COPIES",
                        ErrorMessage = $"LabelCopies must be 1–100. Got: '{labelCopiesStr}'. Defaulting to 1.",
                    });
                    // Non-fatal — still add the row with default copies.
                }
            }

            validRows.Add(new ShipmentCsvRow
            {
                RowNumber = dataRow,
                CustomerCode = customerCode!,
                PartNo = partNo!,
                ProductName = productName!,
                Description = description,
                Quantity = quantity,
                PoNumber = poNumber,
                PoItem = poItem,
                DueDate = dueDate,
                RunNo = runNo,
                Store = store,
                Remarks = remarks,
                LabelCopies = labelCopies,
            });
        }

        LogParseComplete(logger, dataRow, validRows.Count, errors.Count);

        return new ShipmentCsvParseResult
        {
            TotalRows = dataRow,
            ValidRows = validRows,
            Errors = errors,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string? GetField(List<string> fields, int index)
    {
        if (index >= fields.Count)
            return null;

        var value = fields[index].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Split a CSV line respecting double-quoted fields.
    /// </summary>
    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Escaped quote ""
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────

    private static void LogHeaderSkipped(ILogger logger, string headerLine) => logger.LogDebug("Shipment CSV header skipped: {HeaderLine}", headerLine);

    private static void LogParseComplete(ILogger logger, int totalRows, int validCount, int errorCount) => logger.LogInformation("Shipment CSV parse complete: {TotalRows} rows, {ValidCount} valid, {ErrorCount} errors", totalRows, validCount, errorCount);
}

