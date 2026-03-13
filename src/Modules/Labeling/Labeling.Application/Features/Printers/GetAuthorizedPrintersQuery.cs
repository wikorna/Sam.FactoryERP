using MediatR;

namespace Labeling.Application.Features.Printers;

public record GetAuthorizedPrintersQuery() : IRequest<IEnumerable<Guid>>;

