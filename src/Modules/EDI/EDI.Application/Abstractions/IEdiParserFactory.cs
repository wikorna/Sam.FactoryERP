using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

public interface IEdiParserFactory
{
    IEdiParser<TRecord> GetParser<TRecord>(PartnerProfile partner);
}
