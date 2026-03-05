using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

public interface IEdiStagingFileRepository
{
    Task AddAsync(EdiStagingFile stagingFile, CancellationToken ct);
    Task<EdiStagingFile?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<EdiStagingFile?> GetByIdWithErrorsAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(EdiStagingFile stagingFile, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task AddRowsAsync(IEnumerable<EdiStagingRow> rows, CancellationToken ct);
    Task AddErrorsAsync(IEnumerable<EdiStagingFileError> errors, CancellationToken ct);
    Task<int> GetStagingRowCountAsync(Guid stagingId, CancellationToken ct);
    Task<IReadOnlyList<EdiStagingRow>> GetStagingRowsAsync(Guid stagingId, int skip, int take, CancellationToken ct);
}
