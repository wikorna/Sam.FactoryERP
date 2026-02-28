//namespace Sam.FactoryErp.Edi.Application.Features.ReceiveEdiFile;

using MediatR;

namespace EDI.Application.Features.ReceiveEdiFile;

public sealed record ReceiveEdiFileCommand(string PartnerCode, string FullPath, string FileName)
    : IRequest<Guid>;

