using System.Text;

namespace EDI.Application.Utilities;

/// <summary>
/// Lightweight CSV reader utilities used by application-layer handlers.
/// These helpers contain no infrastructure dependencies.
/// </summary>
public static class CsvReaderUtility
{
    /// <summary>
    /// Read the first <paramref name="maxLines"/> raw lines from a stream.
    /// Handles UTF-8 BOM automatically. Stream is left open.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ReadRawLinesAsync(
        Stream stream,
        int maxLines,
        CancellationToken ct)
    {
        var lines = new List<string>(maxLines);

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
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

    /// <summary>
    /// Split a single CSV line respecting double-quoted fields (RFC 4180 compliant).
    /// Handles escaped quotes (<c>""</c> inside quoted fields) and preserves embedded delimiters.
    /// </summary>
    public static List<string> SplitLine(string line, char delimiter)
    {
        var fields  = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Look ahead: if next char is also '"', it's an escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip the second quote
                    }
                    else
                    {
                        inQuotes = false; // closing quote
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')            inQuotes = true;
                else if (c == delimiter) { fields.Add(current.ToString()); current.Clear(); }
                else                     current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}

