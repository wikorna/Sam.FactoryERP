using MediatR;

namespace EDI.Application.Features.Files.GetEdiFilePreview;

public sealed record GetEdiFilePreviewQuery(Guid StagingId, int PageNumber = 1, int PageSize = 50) : IRequest<GetEdiFilePreviewResult?>;
