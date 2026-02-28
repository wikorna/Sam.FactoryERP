using EDI.Application.Abstractions;
using EDI.Domain.Entities;

namespace EDI.Infrastructure;

internal sealed class EdiParserFactory : IEdiParserFactory
{
    private readonly IReadOnlyCollection<IEdiParser> _parsers;

    public EdiParserFactory(IEnumerable<IEdiParser> parsers)
        => _parsers = parsers.ToList();

    public IEdiParser<TRecord> GetParser<TRecord>(PartnerProfile partner)
    {
        var parser = _parsers
            .OfType<IEdiParser<TRecord>>()
            .FirstOrDefault(p => p.CanHandle(partner));

        return parser ?? throw new NotSupportedException(
            $"No EDI parser for record={typeof(TRecord).Name}, " +
            $"partner={partner.PartnerCode}, format={partner.Format}, schema={partner.SchemaVersion}");
    }
}
