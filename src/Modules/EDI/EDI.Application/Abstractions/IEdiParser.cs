using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

public interface IEdiParser
{
    Type RecordType { get; }

    bool CanHandle(PartnerProfile partner);
}

public interface IEdiParser<TRecord> : IEdiParser
{
    IAsyncEnumerable<TRecord> ParseAsync(Stream stream, CancellationToken ct);
}
