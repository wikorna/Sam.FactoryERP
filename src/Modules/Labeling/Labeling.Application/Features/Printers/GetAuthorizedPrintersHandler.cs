using Labeling.Application.Interfaces;
using MediatR;

namespace Labeling.Application.Features.Printers;


public class GetAuthorizedPrintersHandler : IRequestHandler<GetAuthorizedPrintersQuery, IEnumerable<Guid>>
{
    private readonly IPrinterAccessService _accessService;

    public GetAuthorizedPrintersHandler(IPrinterAccessService accessService)
    {
        _accessService = accessService;
    }

    public async Task<IEnumerable<Guid>> Handle(GetAuthorizedPrintersQuery request, CancellationToken cancellationToken)
    {
        return await _accessService.GetAuthorizedPrinterIdsAsync(cancellationToken);
    }
}

