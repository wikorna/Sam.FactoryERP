using MediatR;

namespace EDI.Application.Features.Files.StageEdiFile;

public record StageEdiFileCommand(
    Stream Content,
    string FileName,
    long SizeBytes,
    string? ContentType,
    string? ClientId) : IRequest<StageEdiFileResult>;
