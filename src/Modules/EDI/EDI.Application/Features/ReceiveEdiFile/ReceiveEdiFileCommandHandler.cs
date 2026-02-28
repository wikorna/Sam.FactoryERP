using System.Security.Cryptography;
using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using FactoryERP.Abstractions.Caching;
using MediatR;

namespace EDI.Application.Features.ReceiveEdiFile;

public sealed class ReceiveEdiFileCommandHandler(
    IEdiFileStore fileStore,
    IEdiFileJobRepository jobs,
    IOutboxPublisher outbox,
    ICacheService cache)
    : IRequestHandler<ReceiveEdiFileCommand, Guid>
{
    public async Task<Guid> Handle(ReceiveEdiFileCommand request, CancellationToken cancellationToken)
    {
        EdiFileRef file = new(request.PartnerCode, request.FileName, request.FullPath);

        file = await fileStore.MoveToProcessingAsync(file, cancellationToken).ConfigureAwait(false);

        long sizeBytes = await fileStore.GetSizeAsync(file, cancellationToken).ConfigureAwait(false);

        string sha256 = await ComputeSha256Async(file, cancellationToken).ConfigureAwait(false);

        bool exists = await jobs.ExistsByChecksumAsync(request.PartnerCode, sha256, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            await fileStore.MoveToArchiveAsync(file, cancellationToken).ConfigureAwait(false);
            return Guid.Empty; // duplicate (policy: ignore)
        }

        var partner = await jobs.GetPartnerProfileAsync(request.PartnerCode, cancellationToken).ConfigureAwait(false);

        Guid jobId = Guid.NewGuid();
        EdiFileJob job = EdiFileJob.CreateReceived(
            jobId,
            partner.PartnerCode,
            file.FileName,
            file.FullPath,
            sizeBytes,
            sha256,
            partner.Format,
            partner.SchemaVersion);

        await jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);

        // Invalidate job list caches so dashboards reflect the new job
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken).ConfigureAwait(false);

        foreach (var ev in job.DomainEvents)
        {
            await outbox.EnqueueAsync(ev, cancellationToken).ConfigureAwait(false);
        }

        job.ClearDomainEvents();
        return jobId;
    }

    private async Task<string> ComputeSha256Async(EdiFileRef file, CancellationToken ct)
    {
        await using Stream stream = await fileStore.OpenReadAsync(file, ct).ConfigureAwait(false);
        using SHA256 sha = SHA256.Create();
        byte[] hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
