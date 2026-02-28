using EDI.Application.Abstractions;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.Persistence.Repositories;

public class EdiFileJobRepository(EdiDbContext context) : IEdiFileJobRepository
{
    public async Task<bool> ExistsByChecksumAsync(string partnerCode, string sha256, CancellationToken ct)
    {
        return await context.EdiFileJobs
            .AnyAsync(x => x.PartnerCode == partnerCode && x.Sha256 == sha256, ct);
    }

    public async Task AddAsync(EdiFileJob job, CancellationToken ct)
    {
        await context.EdiFileJobs.AddAsync(job, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<EdiFileJob?> GetAsync(Guid jobId, CancellationToken ct)
    {
        return await context.EdiFileJobs
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);
    }

    public async Task SaveAsync(EdiFileJob job, CancellationToken ct)
    {
        context.EdiFileJobs.Update(job);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<EdiFileJob> Jobs, int TotalCount)> GetJobsAsync(
        string? partnerCode,
        EdiFileJobStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        var query = context.EdiFileJobs.AsQueryable();

        if (!string.IsNullOrEmpty(partnerCode))
            query = query.Where(x => x.PartnerCode == partnerCode);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        var jobs = await query
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (jobs, totalCount);
    }

    public async Task<PartnerProfile> GetPartnerProfileAsync(string partnerCode, CancellationToken ct)
    {
        var profile = await context.PartnerProfiles
            .FirstOrDefaultAsync(x => x.PartnerCode == partnerCode, ct);

        if (profile is null)
            throw new InvalidOperationException($"Partner profile not found: {partnerCode}");

        return profile;
    }
}
