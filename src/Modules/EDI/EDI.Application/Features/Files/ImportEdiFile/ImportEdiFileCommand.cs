using MediatR;

namespace EDI.Application.Features.Files.ImportEdiFile;

public sealed record ImportEdiFileCommand(Guid StagingId, string? ClientId) : IRequest<ImportEdiFileResult>;
