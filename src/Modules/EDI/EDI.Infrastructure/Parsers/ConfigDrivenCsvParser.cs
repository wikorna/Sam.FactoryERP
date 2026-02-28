using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Parsers;

/// <summary>
/// Config-driven CSV parser that uses <see cref="EdiFileTypeConfig"/> column definitions
/// to parse any file type without code changes.
/// </summary>
public sealed partial class ConfigDrivenCsvParser(ILogger<ConfigDrivenCsvParser> logger)
{
    /// <summary>
    /// Parse a CSV stream into generic staging rows using the provided config.
    /// </summary>
    public async IAsyncEnumerable<EdiStagingRow> ParseAsync(
        Stream stream,
        Guid jobId,
        EdiFileTypeConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(config);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var columns = config.Columns.OrderBy(c => c.Ordinal).ToList();
        char delimiter = config.Delimiter.Length > 0 ? config.Delimiter[0] : ',';

        int lineNo = 0;
        int dataRowIndex = 0;
        int totalSkip = config.SkipLines + (config.HasHeaderRow ? config.HeaderLineCount : 0);

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            // Skip header/metadata lines
            if (lineNo <= totalSkip)
                continue;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            dataRowIndex++;
            yield return ParseLine(line, dataRowIndex, jobId, config.FileTypeCode, columns, delimiter);
        }

        LogParseComplete(logger, config.FileTypeCode, dataRowIndex);
    }

    /// <summary>
    /// Read the first N lines from the stream for preview purposes.
    /// Does NOT skip header lines — returns everything for display.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ReadRawLinesAsync(
        Stream stream,
        int maxLines,
        CancellationToken ct)
    {
        var lines = new List<string>(maxLines);

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        string? line;
        while (lines.Count < maxLines &&
               (line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static EdiStagingRow ParseLine(
        string line,
        int rowIndex,
        Guid jobId,
        string fileTypeCode,
        List<EdiColumnDefinition> columns,
        char delimiter)
    {
        // Simple CSV split (handles basic cases; for production with quoted fields, use a library)
        var fields = SplitCsvLine(line, delimiter);

        var parsed = new Dictionary<string, string?>(columns.Count);

        for (int i = 0; i < columns.Count; i++)
        {
            string? value = i < fields.Count ? fields[i] : null;
            parsed[columns[i].ColumnName] = string.IsNullOrEmpty(value) ? null : value.Trim();
        }

        return new EdiStagingRow
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            FileTypeCode = fileTypeCode,
            RowIndex = rowIndex,
            IsSelected = true,
            RawLine = line,
            ParsedColumnsJson = JsonSerializer.Serialize(parsed),
            IsValid = true // Validation runs separately
        };
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
            else
            {
                if (c == '"')
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
        }

        fields.Add(current.ToString());
        return fields;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Config-driven CSV parse complete: FileType={FileTypeCode}, Rows={RowCount}")]
    private static partial void LogParseComplete(ILogger logger, string fileTypeCode, int rowCount);
}

