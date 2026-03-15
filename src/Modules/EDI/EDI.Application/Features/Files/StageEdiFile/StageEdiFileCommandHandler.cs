using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using EDI.Domain.Enums;
using EDI.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.Files.StageEdiFile;

public sealed class StageEdiFileCommandHandler(
    IEdiFileDetector fileDetector,
    IEdiStorageService storageService,
    IEdiStagingFileRepository repository,
    IOutboxPublisher outboxPublisher,
    ILogger<StageEdiFileCommandHandler> logger)
    : IRequestHandler<StageEdiFileCommand, StageEdiFileResult>
{
    private static void LogStaging(ILogger logger, string fileName, long sizeBytes) => logger.LogInformation("Staging EDI file: {FileName} ({SizeBytes} bytes)", fileName, sizeBytes);

    public async Task<StageEdiFileResult> Handle(StageEdiFileCommand request, CancellationToken cancellationToken)
    {
        LogStaging(logger, request.FileName, request.SizeBytes);

        // 1. Detect file type and schema (Header check)
        var detectResult = await fileDetector.DetectAsync(
            request.FileName,
            request.Content,
            request.SizeBytes,
            request.ClientId,
            cancellationToken);

        // We must reset the stream position because the fileDetector reads the header.
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        // 2. Save raw file computing SHA256 during transit
        var storedObj = await storageService.SaveAsync(request.Content, request.FileName, cancellationToken);

        // 3. Create Entity
        var fileType = detectResult.Detected ? detectResult.FileType : EdiFileType.Unknown;
        var stagingFile = new EdiStagingFile
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            OriginalFileName = request.FileName,
            FileType = fileType,
            SchemaKey = detectResult.SchemaKey ?? string.Empty,
            SchemaVersion = detectResult.SchemaVersion ?? string.Empty,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            Sha256 = storedObj.Sha256,
            StorageProvider = storedObj.ProviderName,
            StorageKey = storedObj.StorageKey,
            UploadedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Status = EdiStagingStatus.Staged,
            ProgressPercent = 0,
            DetectResultJson = JsonSerializer.Serialize(detectResult)
        };

        if (!detectResult.Detected)
        {
            stagingFile.Status = EdiStagingStatus.Failed;
            stagingFile.ErrorCode = "DetectionFailed";
            stagingFile.ErrorMessage = detectResult.Errors.Count > 0 ? detectResult.Errors[0].Message : "File detection failed.";
        }

        await repository.AddAsync(stagingFile, cancellationToken);

        // 4. Outbox Event
        if (detectResult.Detected)
        {
            var domainEvent = new EdiStagingRequestedEvent(stagingFile.Id, DateTime.UtcNow, request.ClientId);
            await outboxPublisher.EnqueueAsync(domainEvent, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new StageEdiFileResult(
            StagingId: stagingFile.Id,
            Status: stagingFile.Status,
            Sha256: storedObj.Sha256,
            Size: request.SizeBytes,
            FileName: request.FileName,
            FileType: fileType.ToString(),
            SchemaKey: stagingFile.SchemaKey,
            SchemaVersion: stagingFile.SchemaVersion);
    }
}
