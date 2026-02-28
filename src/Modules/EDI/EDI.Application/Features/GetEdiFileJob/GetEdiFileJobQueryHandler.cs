using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Application.Features.GetEdiFileJobs;
using FactoryERP.Abstractions.Caching;
using MediatR;

namespace EDI.Application.Features.GetEdiFileJob;

public sealed class GetEdiFileJobQueryHandler(IEdiFileJobRepository jobs, ICacheService cache)
    : IRequestHandler<GetEdiFileJobQuery, EdiFileJobDto?>
{
    public async Task<EdiFileJobDto?> Handle(GetEdiFileJobQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = EdiCacheKeys.JobById(request.JobId);
        var settings = EdiCacheKeys.JobDetail(request.JobId);

        return await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var job = await jobs.GetAsync(request.JobId, ct);

                if (job is null)
                    return null!;

                return new EdiFileJobDto(
                    job.Id,
                    job.PartnerCode,
                    job.FileName,
                    job.SizeBytes,
                    job.Format.Value,
                    job.SchemaVersion.Value,
                    job.ReceivedAtUtc,
                    job.AppliedAtUtc,
                    job.Status.ToString(),
                    job.ErrorCode,
                    job.ErrorMessage,
                    job.ParsedRecords,
                    job.AppliedRecords
                );
            },
            settings,
            cancellationToken);
    }
}
