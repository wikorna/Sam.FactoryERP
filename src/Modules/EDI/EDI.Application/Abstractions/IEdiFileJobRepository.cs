using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

public interface IEdiFileJobRepository
{
    Task<bool> ExistsByChecksumAsync(string partnerCode, string sha256, CancellationToken ct);
    Task AddAsync(EdiFileJob job, CancellationToken ct);
    Task<EdiFileJob?> GetAsync(Guid jobId, CancellationToken ct);
    Task SaveAsync(EdiFileJob job, CancellationToken ct);

    Task<(IReadOnlyList<EdiFileJob> Jobs, int TotalCount)> GetJobsAsync(
        string? partnerCode,
        EdiFileJobStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken ct);

    Task<PartnerProfile> GetPartnerProfileAsync(string partnerCode, CancellationToken ct);
}
