using EDI.Application.Abstractions;
using EDI.Application.Features.Files.GetEdiFileStatus;
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

    public async Task<GetEdiFileStatusResult?> GetStatusAsync(Guid id, int maxErrors, CancellationToken ct)
    {
        var file = await dbContext.EdiStagingFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (file is null) return null;

        var errorCount = await dbContext.EdiStagingFileErrors
            .AsNoTracking()
            .CountAsync(x => x.StagingFileId == id, ct);

        var errors = await dbContext.EdiStagingFileErrors
            .AsNoTracking()
            .Where(x => x.StagingFileId == id)
            .OrderBy(x => x.RowNumber)
            .Take(maxErrors)
            .Select(e => new EdiFileErrorSummary(
                e.Code,
                e.Message,
                e.RowNumber,
                e.ColumnName,
                e.Severity.ToString()
            ))
            .ToListAsync(ct);

        return new GetEdiFileStatusResult(
            StagingId: file.Id,
            Status: file.Status.ToString(),
            ProgressPercent: file.ProgressPercent,
            RowCountTotal: file.RowCountTotal,
            RowCountProcessed: file.RowCountProcessed,
            FileName: file.OriginalFileName,
            ErrorCode: file.ErrorCode,
            ErrorMessage: file.ErrorMessage,
            ErrorCount: errorCount,
            Errors: errors
        );
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
