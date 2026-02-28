using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Application.Caching;
using FactoryERP.Abstractions.Caching;
using MediatR;

namespace EDI.Application.Features.GetEdiFileJobs;

public sealed class GetEdiFileJobsQueryHandler(IEdiFileJobRepository jobs, ICacheService cache)
    : IRequestHandler<GetEdiFileJobsQuery, GetEdiFileJobsResponse>
{
    public async Task<GetEdiFileJobsResponse> Handle(GetEdiFileJobsQuery request, CancellationToken cancellationToken)
    {
        var filterHash = ComputeFilterHash(request);
        var cacheKey = EdiCacheKeys.JobsList(filterHash);
        var settings = EdiCacheKeys.JobList();

        return await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var (jobsList, totalCount) = await jobs.GetJobsAsync(
                    request.PartnerCode,
                    request.Status,
                    request.PageNumber,
                    request.PageSize,
                    ct);

                var dtos = jobsList.Select(j => new EdiFileJobDto(
                    j.Id,
                    j.PartnerCode,
                    j.FileName,
                    j.SizeBytes,
                    j.Format.Value,
                    j.SchemaVersion.Value,
                    j.ReceivedAtUtc,
                    j.AppliedAtUtc,
                    j.Status.ToString(),
                    j.ErrorCode,
                    j.ErrorMessage,
                    j.ParsedRecords,
                    j.AppliedRecords
                )).ToList();

                return new GetEdiFileJobsResponse(dtos, totalCount, request.PageNumber, request.PageSize);
            },
            settings,
            cancellationToken);
    }

    private static string ComputeFilterHash(GetEdiFileJobsQuery request)
    {
        var json = JsonSerializer.Serialize(new
        {
            request.PartnerCode,
            Status = request.Status?.ToString(),
            request.PageNumber,
            request.PageSize
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hash)[..8];
    }
}
