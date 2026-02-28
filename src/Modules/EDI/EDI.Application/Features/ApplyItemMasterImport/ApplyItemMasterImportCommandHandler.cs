using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using FactoryERP.Abstractions.Caching;
using MediatR;

namespace EDI.Application.Features.ApplyItemMasterImport;

public sealed class ApplyItemMasterImportCommandHandler(
    IEdiFileJobRepository jobs,
    IStagingRepository staging,
    IItemMasterApplyService applyService,
    IOutboxPublisher outbox,
    IEdiFileStore fileStore,
    ICacheService cache)
    : IRequestHandler<ApplyItemMasterImportCommand, int>
{
    public async Task<int> Handle(ApplyItemMasterImportCommand request, CancellationToken cancellationToken)
    {
        EdiFileJob job = await jobs.GetAsync(request.JobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        job.MarkApplying();
        await jobs.SaveAsync(job, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ItemMasterStagingRow> rows = await staging.GetItemMasterRowsAsync(job.Id, cancellationToken).ConfigureAwait(false);

        int applied = await applyService.ApplyAsync(job.Id, rows, cancellationToken).ConfigureAwait(false);

        job.MarkApplied(applied);
        await jobs.SaveAsync(job, cancellationToken).ConfigureAwait(false);

        // Invalidate job caches after state transition (Applying → Applied)
        await cache.RemoveAsync(EdiCacheKeys.JobById(job.Id), cancellationToken).ConfigureAwait(false);
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken).ConfigureAwait(false);

        foreach (var ev in job.DomainEvents)
        {
            await outbox.EnqueueAsync(ev, cancellationToken).ConfigureAwait(false);
        }

        job.ClearDomainEvents();

        EdiFileRef file = new(job.PartnerCode, job.FileName, job.SourcePath);
        await fileStore.MoveToArchiveAsync(file, cancellationToken).ConfigureAwait(false);

        return applied;
    }
}
