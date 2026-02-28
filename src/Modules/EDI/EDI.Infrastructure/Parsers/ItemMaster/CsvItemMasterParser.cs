using System.Runtime.CompilerServices;
using System.Text;
using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using EDI.Domain.ValueObjects;

namespace EDI.Infrastructure.Parsers.ItemMaster;

internal sealed class CsvItemMasterParser : IEdiParser<ItemMasterStagingRow>
{
    public Type RecordType => typeof(ItemMasterStagingRow);

    public bool CanHandle(PartnerProfile partner)
        => partner.Format == EdiFormat.Csv
           && partner.SchemaVersion == EdiSchemaVersion.V1; // ใช้ constant ที่มีจริง

    public async IAsyncEnumerable<ItemMasterStagingRow> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        int lineNo = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return ParseLine(line, lineNo);
        }
    }

    private static ItemMasterStagingRow ParseLine(string line, int lineNo)
        => throw new NotImplementedException(
            $"ItemMaster CSV parsing not implemented (line {lineNo}).");
}
