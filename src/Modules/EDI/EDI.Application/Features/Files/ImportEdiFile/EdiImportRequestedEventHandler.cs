using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using EDI.Domain.Enums;
using EDI.Domain.Events;
using EDI.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.Files.ImportEdiFile;

/// <summary>
/// Handles <see cref="EdiImportRequestedEvent"/> published by the outbox processor.
/// Streams the staged file, parses rows using the config-driven parser, validates each row,
/// persists results in batches, and updates progress accurately.
/// </summary>
public sealed partial class EdiImportRequestedHandler(
    IEdiStagingFileRepository repository,
    IEdiStorageService storageService,
    IEdiFileTypeConfigRepository fileTypeConfigRepository,
    ILogger<EdiImportRequestedHandler> logger)
    : INotificationHandler<EdiImportRequestedEvent>
{
    private const int BatchSize = 100;

    public async Task Handle(EdiImportRequestedEvent notification, CancellationToken cancellationToken)
    {
        LogProcessingStart(logger, notification.StagingFileId, notification.CorrelationId ?? "-");

        var stagingFile = await repository.GetByIdAsync(notification.StagingFileId, cancellationToken);
        if (stagingFile is null)
        {
            LogStagingFileNotFound(logger, notification.StagingFileId);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await RunImportAsync(stagingFile, cancellationToken);
            sw.Stop();
            LogProcessingComplete(logger, notification.StagingFileId, stagingFile.RowCountTotal ?? 0, sw.ElapsedMilliseconds);
        }
        catch (EdiImportFailedException ex)
        {
            sw.Stop();
            LogImportFailed(logger, ex, notification.StagingFileId, ex.ErrorCode);
            await MarkFailedAsync(stagingFile, ex.ErrorCode, ex.Message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            LogImportCancelled(logger, notification.StagingFileId);
            await MarkFailedAsync(stagingFile, "ImportCancelled", "Import was cancelled.", cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogProcessingError(logger, ex, notification.StagingFileId);
            await MarkFailedAsync(stagingFile, "ImportFailed", ex.Message, cancellationToken);
        }
    }

    private async Task RunImportAsync(EdiStagingFile stagingFile, CancellationToken ct)
    {
        // ── 1. Transition to Processing ────────────────────────────────────────
        stagingFile.Status = EdiStagingStatus.Processing;
        stagingFile.ProgressPercent = 0;
        await repository.UpdateAsync(stagingFile, ct);
        await repository.SaveChangesAsync(ct);

        // ── 2. Load file type config (required for column mapping) ─────────────
        var fileTypeCode = stagingFile.FileType.ToString();
        var config = await fileTypeConfigRepository.GetByCodeAsync(fileTypeCode, ct);

        // If config not found, fall back to a minimal raw-line import
        // so the import can still proceed and rows are visible for review.
        bool useConfigParser = config is not null && config.Columns.Count > 0;

        // ── 3. Open stored file stream ─────────────────────────────────────────
        await using var stream = await storageService.OpenReadAsync(stagingFile.StorageKey, ct);

        // ── 4. Stream-parse and batch-insert ──────────────────────────────────
        int totalRows = 0;
        int processedRows = 0;
        int errorCount = 0;

        var rowBatch = new List<EdiStagingRow>(BatchSize);
        var errorBatch = new List<EdiStagingFileError>(BatchSize);

        if (useConfigParser)
        {
            // Config-driven path: proper column mapping + validation
            await foreach (var row in ParseWithConfigAsync(stagingFile, config!, stream, ct))
            {
                ct.ThrowIfCancellationRequested();
                totalRows++;

                var validationErrors = ValidateRow(row, config!);
                if (validationErrors.Count > 0)
                {
                    row.IsValid = false;
                    row.ValidationErrorsJson = JsonSerializer.Serialize(validationErrors);
                    errorCount++;

                    errorBatch.AddRange(validationErrors.Select(e => new EdiStagingFileError
                    {
                        Id = Guid.NewGuid(),
                        StagingFileId = stagingFile.Id,
                        Code = e.Code,
                        Message = e.Message,
                        RowNumber = row.RowIndex,
                        ColumnName = e.ColumnName,
                        Severity = EdiSeverity.Error,
                        CreatedAtUtc = DateTime.UtcNow
                    }));
                }

                rowBatch.Add(row);

                if (rowBatch.Count >= BatchSize)
                {
                    await FlushBatchAsync(rowBatch, errorBatch, repository, ct);
                    processedRows += rowBatch.Count;
                    rowBatch.Clear();
                    errorBatch.Clear();

                    // Update progress: cap at 99% until we've fully flushed
                    stagingFile.ProgressPercent = Math.Min(99, (int)(processedRows * 100.0 / Math.Max(totalRows, 1)));
                    await repository.UpdateAsync(stagingFile, ct);
                    await repository.SaveChangesAsync(ct);

                    LogBatchProgress(logger, stagingFile.Id, processedRows, totalRows);
                }
            }
        }
        else
        {
            // Raw fallback: no config available, store raw lines only
            await foreach (var row in ParseRawAsync(stagingFile, stream, ct))
            {
                ct.ThrowIfCancellationRequested();
                totalRows++;
                rowBatch.Add(row);

                if (rowBatch.Count >= BatchSize)
                {
                    await FlushBatchAsync(rowBatch, errorBatch, repository, ct);
                    processedRows += rowBatch.Count;
                    rowBatch.Clear();

                    stagingFile.ProgressPercent = Math.Min(99, (int)(processedRows * 100.0 / Math.Max(totalRows, 1)));
                    await repository.UpdateAsync(stagingFile, ct);
                    await repository.SaveChangesAsync(ct);
                }
            }
        }

        // Flush remainder
        if (rowBatch.Count > 0)
        {
            await FlushBatchAsync(rowBatch, errorBatch, repository, ct);
            processedRows += rowBatch.Count;
        }

        // ── 5. Mark Completed ──────────────────────────────────────────────────
        stagingFile.RowCountTotal = totalRows;
        stagingFile.RowCountProcessed = processedRows;
        stagingFile.Status = EdiStagingStatus.Completed;
        stagingFile.ProgressPercent = 100;
        stagingFile.ErrorCode = errorCount > 0 ? "RowErrors" : null;
        stagingFile.ErrorMessage = errorCount > 0 ? $"{errorCount} row(s) failed validation." : null;
        stagingFile.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateAsync(stagingFile, ct);
        await repository.SaveChangesAsync(ct);
    }

    // ── Config-driven streaming parser ────────────────────────────────────────

    private static async IAsyncEnumerable<EdiStagingRow> ParseWithConfigAsync(
        EdiStagingFile stagingFile,
        EdiFileTypeConfig config,
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var columns = config.Columns.OrderBy(c => c.Ordinal).ToList();
        char delimiter = config.Delimiter.Length > 0 ? config.Delimiter[0] : ',';
        int totalSkip = config.SkipLines + (config.HasHeaderRow ? config.HeaderLineCount : 0);

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(false, false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        int lineNo = 0;
        int dataRowIndex = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            if (lineNo <= totalSkip) continue;
            if (string.IsNullOrWhiteSpace(line)) continue;

            dataRowIndex++;
            var fields = SplitCsvLine(line, delimiter);
            var parsed = new Dictionary<string, string?>(columns.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columns.Count; i++)
            {
                var value = i < fields.Count ? fields[i].Trim() : null;
                parsed[columns[i].ColumnName] = string.IsNullOrEmpty(value) ? null : value;
            }

            yield return new EdiStagingRow
            {
                Id = Guid.NewGuid(),
                JobId = stagingFile.Id,
                FileTypeCode = stagingFile.FileType.ToString(),
                RowIndex = dataRowIndex,
                IsSelected = true,
                RawLine = line,
                ParsedColumnsJson = JsonSerializer.Serialize(parsed),
                IsValid = true
            };
        }
    }

    // ── Raw fallback parser (no config) ──────────────────────────────────────

    private static async IAsyncEnumerable<EdiStagingRow> ParseRawAsync(
        EdiStagingFile stagingFile,
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        int lineNo = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            lineNo++;

            yield return new EdiStagingRow
            {
                Id = Guid.NewGuid(),
                JobId = stagingFile.Id,
                FileTypeCode = stagingFile.FileType.ToString(),
                RowIndex = lineNo,
                IsSelected = true,
                RawLine = line,
                ParsedColumnsJson = "{}",
                IsValid = true
            };
        }
    }

    // ── Row validation ────────────────────────────────────────────────────────

    private static List<RowValidationError> ValidateRow(EdiStagingRow row, EdiFileTypeConfig config)
    {
        if (string.IsNullOrEmpty(row.ParsedColumnsJson) || row.ParsedColumnsJson == "{}")
            return [];

        Dictionary<string, string?>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.ParsedColumnsJson);
        }
        catch (JsonException)
        {
            return [new("ParseError", null, "Row data could not be parsed as JSON.")];
        }

        if (parsed is null) return [];

        var errors = new List<RowValidationError>();
        foreach (var col in config.Columns.Where(c => c.IsRequired))
        {
            if (!parsed.TryGetValue(col.ColumnName, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new(col.ColumnName, col.ColumnName, $"Required field '{col.ColumnName}' is missing or empty."));
            }
        }

        return errors;
    }

    private sealed record RowValidationError(string Code, string? ColumnName, string Message);

    // ── Batch flush ───────────────────────────────────────────────────────────

    private static async Task FlushBatchAsync(
        List<EdiStagingRow> rows,
        List<EdiStagingFileError> errors,
        IEdiStagingFileRepository repo,
        CancellationToken ct)
    {
        if (rows.Count > 0)
            await repo.AddRowsAsync(rows, ct);

        if (errors.Count > 0)
            await repo.AddErrorsAsync(errors, ct);

        await repo.SaveChangesAsync(ct);
    }

    // ── Failure helper ────────────────────────────────────────────────────────

    private async Task MarkFailedAsync(
        EdiStagingFile stagingFile,
        string errorCode,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            stagingFile.Status = EdiStagingStatus.Failed;
            stagingFile.ErrorCode = errorCode;
            stagingFile.ErrorMessage = errorMessage;
            stagingFile.UpdatedAtUtc = DateTime.UtcNow;

            await repository.UpdateAsync(stagingFile, ct);
            await repository.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            LogFailedToMarkFailed(logger, ex, stagingFile.Id);
        }
    }

    // ── CSV splitter (handles quoted fields) ──────────────────────────────────

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
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == delimiter) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI import: starting — StagingId={StagingId}, CorrelationId={CorrelationId}")]
    private static partial void LogProcessingStart(ILogger logger, Guid stagingId, string correlationId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI import: staging file not found — StagingId={StagingId}")]
    private static partial void LogStagingFileNotFound(ILogger logger, Guid stagingId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI import: completed — StagingId={StagingId}, TotalRows={TotalRows}, ElapsedMs={ElapsedMs}")]
    private static partial void LogProcessingComplete(ILogger logger, Guid stagingId, int totalRows, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI import: batch progress — StagingId={StagingId}, Processed={Processed}/{Total}")]
    private static partial void LogBatchProgress(ILogger logger, Guid stagingId, int processed, int total);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI import: domain import failure — StagingId={StagingId}, ErrorCode={ErrorCode}")]
    private static partial void LogImportFailed(ILogger logger, Exception ex, Guid stagingId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI import: cancelled — StagingId={StagingId}")]
    private static partial void LogImportCancelled(ILogger logger, Guid stagingId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI import: unexpected error — StagingId={StagingId}")]
    private static partial void LogProcessingError(ILogger logger, Exception ex, Guid stagingId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI import: failed to mark staging file as failed — StagingId={StagingId}")]
    private static partial void LogFailedToMarkFailed(ILogger logger, Exception ex, Guid stagingId);
}
