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
/// Reads only the header row — never loads the full file into memory.
/// </summary>
public sealed partial class CsvEdiFileDetector(
    IEdiSchemaProvider              schemaProvider,
    ILogger<CsvEdiFileDetector>     logger)
    : IEdiFileDetector
{
    private const long   MaxFileSizeBytes  = 10 * 1024 * 1024; // 10 MB
    private const string CsvExtension      = ".csv";

    public async Task<DetectEdiFileResult> DetectAsync(
        string            fileName,
        Stream            content,
        long              sizeBytes,
        string?           clientId,
        CancellationToken ct)
    {
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

        // ── 5. Read header line ───────────────────────────────────────────────
        IReadOnlyList<string> rawLines;
        try
        {
            rawLines = await CsvReaderUtility.ReadRawLinesAsync(content, maxLines: 2, ct);
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

        // ── 6. Parse and normalize header columns ─────────────────────────────
        var headerLine    = rawLines[0];
        var actualColumns = CsvReaderUtility.SplitLine(headerLine, ',')
            .Select(NormalizeColumn)
            .ToList();

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

        // ── 7. Build header dictionary ────────────────────────────────────────
        var header = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < actualColumns.Count; i++)
            header[actualColumns[i]] = $"col_{i}";

        // ── 8. Success ────────────────────────────────────────────────────────
        var documentNo       = Path.GetFileNameWithoutExtension(fileName);
        var fileTypeName     = fileType.ToString();
        LogDetected(logger, fileName, fileTypeName, schema.SchemaKey);

        return DetectEdiFileResult.Success(
            fileName:            fileName,
            fileType:            fileType,
            fileTypeDisplayName: fileTypeName,
            documentNo:          documentNo,
            schemaKey:           schema.SchemaKey,
            schemaVersion:       schema.SchemaVersion,
            header:             header,
            warnings:           warnings);
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
}

