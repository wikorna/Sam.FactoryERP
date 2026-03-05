using EDI.Domain.Enums;

namespace EDI.Application.Features.Files.StageEdiFile;

public record StageEdiFileResult(
    Guid StagingId,
    EdiStagingStatus Status,
    string Sha256,
    long Size,
    string FileName,
    string FileType,
    string SchemaKey,
    string SchemaVersion);
