using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using EDI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.Persistence.Repositories;

public sealed class EdiFileTypeConfigRepository(EdiDbContext context) : IEdiFileTypeConfigRepository
{
    public async Task<IReadOnlyList<EdiFileTypeConfig>> GetAllActiveAsync(CancellationToken ct)
    {
        return await context.EdiFileTypeConfigs
            .Include(c => c.Columns)
            .Where(c => c.IsActive)
            .OrderBy(c => c.DetectionPriority)
            .ToListAsync(ct);
    }

    public async Task<EdiFileTypeConfig?> GetByCodeAsync(string fileTypeCode, CancellationToken ct)
    {
        return await context.EdiFileTypeConfigs
            .Include(c => c.Columns)
            .FirstOrDefaultAsync(c => c.FileTypeCode == fileTypeCode, ct);
    }
}

