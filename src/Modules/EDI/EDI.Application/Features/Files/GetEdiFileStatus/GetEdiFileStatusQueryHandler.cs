using EDI.Application.Abstractions;
using MediatR;

namespace EDI.Application.Features.Files.GetEdiFileStatus;

public sealed class GetEdiFileStatusQueryHandler(IEdiStagingFileRepository repository)
    : IRequestHandler<GetEdiFileStatusQuery, GetEdiFileStatusResult?>
{
    public async Task<GetEdiFileStatusResult?> Handle(GetEdiFileStatusQuery request, CancellationToken cancellationToken)
    {
        // Use optimized query - status check is frequent, must be lightweight
        return await repository.GetStatusAsync(request.StagingId, maxErrors: 100, cancellationToken);
    }
}


