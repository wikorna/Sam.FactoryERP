using EDI.Application.Abstractions;
using EDI.Domain.Enums;
using EDI.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.Files.ImportEdiFile;

public sealed class ImportEdiFileCommandHandler(
    IEdiStagingFileRepository repository,
    IOutboxPublisher outboxPublisher,
    ILogger<ImportEdiFileCommandHandler> logger)
    : IRequestHandler<ImportEdiFileCommand, ImportEdiFileResult>
{
    private static void LogImporting(ILogger logger, Guid stagingId) => logger.LogInformation("Queuing EDI file import: {StagingId}", stagingId);

    private static void LogInvalidStatus(ILogger logger, Guid stagingId, EdiStagingStatus status) => logger.LogWarning("Cannot import staging file {StagingId} because its status is {Status}", stagingId, status);

    public async Task<ImportEdiFileResult> Handle(ImportEdiFileCommand request, CancellationToken cancellationToken)
    {
        LogImporting(logger, request.StagingId);

        var stagingFile = await repository.GetByIdAsync(request.StagingId, cancellationToken);
        if (stagingFile is null)
        {
            return new ImportEdiFileResult(false, "Staging file not found.");
        }

        // Must be in a valid state to start import
        if (stagingFile.Status != EdiStagingStatus.Staged && stagingFile.Status != EdiStagingStatus.Validated)
        {
            LogInvalidStatus(logger, request.StagingId, stagingFile.Status);
            return new ImportEdiFileResult(false, $"Staging file is currently {stagingFile.Status} and cannot be imported.");
        }

        stagingFile.Status = EdiStagingStatus.Queued;
        
        await repository.UpdateAsync(stagingFile, cancellationToken);

        // Emit outbox event for the background worker to pick up
        var outboxEvent = new EdiImportRequestedEvent(
            StagingFileId: stagingFile.Id,
            RequestedAtUtc: DateTime.UtcNow,
            RequestedByUserId: request.ClientId,
            CorrelationId: Guid.NewGuid().ToString("N"));

        await outboxPublisher.EnqueueAsync(outboxEvent, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new ImportEdiFileResult(true, "File queued for import.");
    }
}
