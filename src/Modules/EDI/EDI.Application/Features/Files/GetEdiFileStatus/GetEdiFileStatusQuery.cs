using MediatR;

namespace EDI.Application.Features.Files.GetEdiFileStatus;

public record GetEdiFileStatusQuery(Guid StagingId) : IRequest<GetEdiFileStatusResult?>;
