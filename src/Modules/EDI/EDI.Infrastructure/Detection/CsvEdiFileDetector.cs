using System.Diagnostics;
using System.Text;
using EDI.Application.Abstractions;
using EDI.Application.Features.DetectEdiFile;
using EDI.Application.Utilities;
using EDI.Domain.Enums;
using EDI.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Detects EDI file type and validates CSV structure against schema.
/// Supports both simple headers (with skip-lines) and segment-marker based headers (H1/S1/S2/S3).
/// Reads only the first N lines — never loads the full file into memory.
/// </summary>
public sealed partial class CsvEdiFileDetector(
    IEdiSchemaProvider              schemaProvider,
    ILogger<CsvEdiFileDetector>     logger)
    : IEdiFileDetector
{
    private const long   MaxFileSizeBytes  = 10 * 1024 * 1024; // 10 MB
    private const string CsvExtension      = ".csv";
    private const int    MaxLinesToRead    = 20; // Enough to find header in any SAP format

    public async Task<DetectEdiFileResult> DetectAsync(
        string            fileName,
        Stream            content,
        long              sizeBytes,
        string?           clientId,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors   = new List<EdiDetectionError>();
        var warnings = new List<string>();

        // ── 1. Size guard ─────────────────────────────────────────────────────
        LogDetecting(logger, fileName, sizeBytes, clientId ?? "-");

        if (sizeBytes > MaxFileSizeBytes)
        {
            LogFileTooLarge(logger, fileName, sizeBytes, MaxFileSizeBytes);
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.FileTooLarge,
                $"File size {sizeBytes:N0} bytes exceeds the 10 MB limit."));
            return DetectEdiFileResult.Failure(fileName, EdiFileType.Unknown, errors);
        }

        // ── 2. Extension ─────────────────────────────────────────────────────
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(CsvExtension, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.InvalidExtension,
                $"File must be a .CSV file. Received extension: '{extension}'."));
            return DetectEdiFileResult.Failure(fileName, EdiFileType.Unknown, errors);
        }

        // ── 3. Filename prefix → EdiFileType ──────────────────────────────────
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var fileType = ResolveFileType(baseName);

        if (fileType == EdiFileType.Unknown)
        {
            LogInvalidFilename(logger, fileName);
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.InvalidFilename,
                "File name must start with 'F' (Forecast) or 'P' (Purchase Order)."));
            return DetectEdiFileResult.Failure(fileName, EdiFileType.Unknown, errors);
        }

        // ── 4. Load schema ────────────────────────────────────────────────────
        var schema = await schemaProvider.GetSchemaAsync(fileType, ct);
        if (schema is null)
        {
            LogNoSchema(logger, fileName, fileType.ToString());
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.UnknownFileType,
                $"No schema configured for file type '{fileType}'."));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }

        // ── 5. Read enough lines to find the header ───────────────────────────
        IReadOnlyList<string> rawLines;
        try
        {
            rawLines = await CsvReaderUtility.ReadRawLinesAsync(content, MaxLinesToRead, ct);
        }
        catch (DecoderFallbackException ex)
        {
            LogEncodingError(logger, fileName, ex.Message);
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.InvalidEncoding,
                "File contains invalid UTF-8 byte sequences."));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCsvParseError(logger, fileName, ex.Message);
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.CsvParseError,
                $"Failed to read the file: {ex.Message}"));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }

        if (rawLines.Count == 0)
        {
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.EmptyFile,
                "The file is empty or contains no readable lines."));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }

        // ── 6. Locate header row and extract metadata ─────────────────────────
        string? headerLine;
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (schema.HasSegmentMarkers && !string.IsNullOrEmpty(schema.HeaderRowMarker))
        {
            // Segment-marker based: scan for the H1 (or similar) row
            headerLine = FindSegmentHeaderLine(rawLines, schema, metadata);
        }
        else
        {
            // Skip-lines based: skip N metadata lines, then take the header
            headerLine = FindSkipLinesHeaderLine(rawLines, schema, metadata);
        }

        if (headerLine is null)
        {
            var reason = schema.HasSegmentMarkers
                ? $"Could not find header row marker '{schema.HeaderRowMarker}' in the first {rawLines.Count} lines."
                : $"File has fewer lines than the required skip count ({schema.SkipLines}) + header.";

            LogHeaderMismatch(logger, fileName, reason);
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.HeaderMismatch,
                reason));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }

        // ── 7. Parse and normalize header columns ─────────────────────────────
        var actualColumns = CsvReaderUtility.SplitLine(headerLine, ',')
            .Select(NormalizeColumn)
            .ToList();

        // For segment-marker files, the first column is the marker itself — skip it
        if (schema.HasSegmentMarkers && actualColumns.Count > 0)
        {
            actualColumns.RemoveAt(0);
        }

        // Apply alias mapping: resolve alias → canonical name
        var resolvedColumns = actualColumns
            .Select(col => schema.HeaderAliases.TryGetValue(col, out var canonical)
                ? NormalizeColumn(canonical)
                : col)
            .ToList();

        // Normalize required headers for comparison
        var requiredNormalized = schema.RequiredHeaders
            .Select(NormalizeColumn)
            .ToList();

        var missing = requiredNormalized
            .Except(resolvedColumns, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
        {
            LogHeaderMismatch(logger, fileName, string.Join(", ", missing));
            errors.Add(new EdiDetectionError(
                EdiErrorCodes.HeaderMismatch,
                $"CSV header missing required columns: {string.Join(", ", missing)}."));
            return DetectEdiFileResult.Failure(fileName, fileType, errors);
        }

        // Extra columns → warnings only
        var knownNormalized = requiredNormalized
            .Concat(schema.OptionalHeaders.Select(NormalizeColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extra = resolvedColumns
            .Where(c => !string.IsNullOrWhiteSpace(c) && !knownNormalized.Contains(c))
            .ToList();

        if (extra.Count > 0)
            warnings.Add($"File contains extra columns not in schema: {string.Join(", ", extra)}.");

        // Detect duplicate column names in the header
        var duplicates = actualColumns
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            var dupList = string.Join(", ", duplicates);
            LogDuplicateColumns(logger, fileName, dupList);
            warnings.Add($"File contains duplicate column names: {dupList}.");
        }

        // ── 8. Build header dictionary (column positions + metadata) ──────────
        var header = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Add column positions
        for (int i = 0; i < actualColumns.Count; i++)
            header[actualColumns[i]] = $"col_{i}";

        // Merge extracted metadata
        foreach (var (key, value) in metadata)
            header[$"meta:{key}"] = value;

        // ── 9. Success ────────────────────────────────────────────────────────
        var documentNo   = Path.GetFileNameWithoutExtension(fileName);
        var fileTypeName = fileType.ToString();
        var displayName  = schema.DisplayName ?? fileTypeName;

        stopwatch.Stop();
        LogDetected(logger, fileName, fileTypeName, schema.SchemaKey);
        LogDetectionDuration(logger, fileName, stopwatch.ElapsedMilliseconds);

        return DetectEdiFileResult.Success(
            fileName:            fileName,
            fileType:            fileType,
            fileTypeDisplayName: displayName,
            documentNo:          documentNo,
            schemaKey:           schema.SchemaKey,
            schemaVersion:       schema.SchemaVersion,
            header:              header,
            warnings:            warnings);
    }

    // ── Header location strategies ────────────────────────────────────────────

    /// <summary>
    /// For segment-marker based files (PO): scan lines for the header row marker (e.g. H1).
    /// Also extract metadata from S1/S2/S3 rows.
    /// </summary>
    private static string? FindSegmentHeaderLine(
        IReadOnlyList<string> rawLines,
        EdiSchema schema,
        Dictionary<string, string?> metadata)
    {
        string? headerLine = null;
        var markerSet = schema.MetadataRowMarkers is not null
            ? new HashSet<string>(schema.MetadataRowMarkers, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = CsvReaderUtility.SplitLine(line, ',');
            if (fields.Count == 0) continue;

            var marker = NormalizeColumn(fields[0]);

            // Check if this is the header row marker
            if (marker.Equals(schema.HeaderRowMarker, StringComparison.OrdinalIgnoreCase))
            {
                headerLine = line;
                continue; // keep scanning for any remaining metadata
            }

            // Check if this is a metadata row
            if (markerSet.Contains(marker) && schema.MetadataFields is not null)
            {
                if (schema.MetadataFields.TryGetValue(marker, out var fieldNames))
                {
                    for (int i = 0; i < fieldNames.Count && i + 1 < fields.Count; i++)
                    {
                        var fieldName = fieldNames[i];
                        var value = NormalizeColumn(fields[i + 1]); // +1 to skip the marker column
                        metadata[$"{marker}.{fieldName}"] = value;
                    }
                }
            }
        }

        return headerLine;
    }

    /// <summary>
    /// For skip-lines based files (Forecast): skip N metadata lines, then take the header.
    /// Also extract metadata from the skipped lines.
    /// </summary>
    private static string? FindSkipLinesHeaderLine(
        IReadOnlyList<string> rawLines,
        EdiSchema schema,
        Dictionary<string, string?> metadata)
    {
        int skipLines = schema.SkipLines;

        if (rawLines.Count <= skipLines)
            return null;

        // Extract metadata from skipped lines
        for (int lineIdx = 0; lineIdx < skipLines && lineIdx < rawLines.Count; lineIdx++)
        {
            var metaLine = rawLines[lineIdx];
            var fields = CsvReaderUtility.SplitLine(metaLine, ',');

            // Use the metadata field definitions if available
            var lineKey = $"line{lineIdx}";
            if (schema.MetadataFields is not null && schema.MetadataFields.TryGetValue(lineKey, out var fieldNames))
            {
                for (int i = 0; i < fieldNames.Count && i < fields.Count; i++)
                {
                    metadata[$"{fieldNames[i]}"] = NormalizeColumn(fields[i]);
                }
            }
            else
            {
                // Fallback: store raw fields with positional keys
                for (int i = 0; i < fields.Count; i++)
                {
                    metadata[$"line{lineIdx}.field{i}"] = NormalizeColumn(fields[i]);
                }
            }
        }

        return rawLines[skipLines];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EdiFileType ResolveFileType(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName)) return EdiFileType.Unknown;
        return baseName[0] switch
        {
            'F' or 'f' => EdiFileType.Forecast,
            'P' or 'p' => EdiFileType.PurchaseOrder,
            _           => EdiFileType.Unknown,
        };
    }

    /// <summary>Trim whitespace and strip UTF-8 BOM marker from a column name.</summary>
    private static string NormalizeColumn(string col) =>
        col.Trim().TrimStart('\uFEFF').Trim('"');

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "EDI detect: start — {FileName} ({SizeBytes} bytes) clientId={ClientId}")]
    private static partial void LogDetecting(ILogger l, string fileName, long sizeBytes, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: file too large — {FileName} ({SizeBytes} bytes, max {MaxBytes})")]
    private static partial void LogFileTooLarge(ILogger l, string fileName, long sizeBytes, long maxBytes);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: invalid filename prefix — {FileName}")]
    private static partial void LogInvalidFilename(ILogger l, string fileName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: no schema for {FileName} (type={FileType})")]
    private static partial void LogNoSchema(ILogger l, string fileName, string fileType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: encoding error — {FileName}: {Detail}")]
    private static partial void LogEncodingError(ILogger l, string fileName, string detail);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: CSV parse error — {FileName}: {Detail}")]
    private static partial void LogCsvParseError(ILogger l, string fileName, string detail);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: header mismatch — {FileName}, missing={MissingColumns}")]
    private static partial void LogHeaderMismatch(ILogger l, string fileName, string missingColumns);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI detect: success — {FileName} → {FileType} (schemaKey={SchemaKey})")]
    private static partial void LogDetected(ILogger l, string fileName, string fileType, string schemaKey);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "EDI detect: completed in {ElapsedMs}ms for {FileName}")]
    private static partial void LogDetectionDuration(ILogger l, string fileName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI detect: duplicate columns in header — {FileName}, columns={DuplicateColumns}")]
    private static partial void LogDuplicateColumns(ILogger l, string fileName, string duplicateColumns);
}

