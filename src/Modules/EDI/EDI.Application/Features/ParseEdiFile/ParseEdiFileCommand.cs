using MediatR;

namespace EDI.Application.Features.ParseEdiFile;

public sealed record ParseEdiFileCommand(Guid JobId) : IRequest<int>;
