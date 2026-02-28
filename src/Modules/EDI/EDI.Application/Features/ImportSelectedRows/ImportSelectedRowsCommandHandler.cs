using EDI.Application.Abstractions;
using EDI.Application.Caching;
using FactoryERP.Abstractions.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.ImportSelectedRows;

public sealed partial class ImportSelectedRowsCommandHandler(
    IEdiFileJobRepository jobs,
    IStagingRepository staging,
    IEdiFileStore fileStore,
    IOutboxPublisher outbox,
    ICacheService cache,
    ILogger<ImportSelectedRowsCommandHandler> logger)
    : IRequestHandler<ImportSelectedRowsCommand, ImportSelectedRowsResponse>
{
    public async Task<ImportSelectedRowsResponse> Handle(
        ImportSelectedRowsCommand request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(request.JobId, cancellationToken)
                  ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        job.MarkApplying();
        await jobs.SaveAsync(job, cancellationToken);

        // Get only selected + valid rows
        var rows = await staging.GetSelectedValidRowsAsync(job.Id, cancellationToken);
        int totalRows = await staging.GetStagingRowCountAsync(job.Id, cancellationToken);
        int importedCount = rows.Count;
        int skippedCount = totalRows - importedCount;

        // TODO: Process the imported rows — route to appropriate domain service
        // based on job.FileTypeCode. This is the extension point where
        // config-driven rows get mapped to domain commands.

        job.MarkApplied(importedCount);
        await jobs.SaveAsync(job, cancellationToken);

        // Invalidate caches
        await cache.RemoveAsync(EdiCacheKeys.JobById(job.Id), cancellationToken);
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken);

        // Publish domain events
        foreach (var ev in job.DomainEvents)
        {
            await outbox.EnqueueAsync(ev, cancellationToken);
        }

        job.ClearDomainEvents();

        // Archive the file
        var file = new EdiFileRef(job.PartnerCode, job.FileName, job.SourcePath);
        await fileStore.MoveToArchiveAsync(file, cancellationToken);

        LogImportComplete(logger, job.Id, importedCount, skippedCount);

        return new ImportSelectedRowsResponse(
            job.Id, importedCount, skippedCount, job.Status.ToString());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI import complete: JobId={JobId}, Imported={Imported}, Skipped={Skipped}")]
    private static partial void LogImportComplete(ILogger logger, Guid jobId, int imported, int skipped);
}

