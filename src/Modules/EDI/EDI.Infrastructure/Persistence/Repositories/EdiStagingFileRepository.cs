using EDI.Application.Abstractions;
using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.Persistence.Repositories;

public sealed class EdiStagingFileRepository(EdiDbContext dbContext) : IEdiStagingFileRepository
{
    public Task AddAsync(EdiStagingFile stagingFile, CancellationToken ct)
    {
        dbContext.EdiStagingFiles.Add(stagingFile);
        return Task.CompletedTask;
    }

    public Task<EdiStagingFile?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return dbContext.EdiStagingFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<EdiStagingFile?> GetByIdWithErrorsAsync(Guid id, CancellationToken ct)
    {
        return dbContext.EdiStagingFiles
            .Include(x => x.Errors)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task UpdateAsync(EdiStagingFile stagingFile, CancellationToken ct)
    {
        dbContext.EdiStagingFiles.Update(stagingFile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return dbContext.SaveChangesAsync(ct);
    }

    public Task AddRowsAsync(IEnumerable<EdiStagingRow> rows, CancellationToken ct)
    {
        dbContext.EdiStagingRows.AddRange(rows);
        return Task.CompletedTask;
    }

    public Task AddErrorsAsync(IEnumerable<EdiStagingFileError> errors, CancellationToken ct)
    {
        dbContext.EdiStagingFileErrors.AddRange(errors);
        return Task.CompletedTask;
    }

    public Task<int> GetStagingRowCountAsync(Guid stagingId, CancellationToken ct)
    {
        return dbContext.EdiStagingRows.CountAsync(x => x.JobId == stagingId, ct);
    }

    public async Task<IReadOnlyList<EdiStagingRow>> GetStagingRowsAsync(Guid stagingId, int skip, int take, CancellationToken ct)
    {
        return await dbContext.EdiStagingRows
            .AsNoTracking()
            .Where(x => x.JobId == stagingId)
            .OrderBy(x => x.RowIndex)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
