using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

/// <summary>
/// Repository for file type configuration and generic staging rows.
/// </summary>
public interface IEdiFileTypeConfigRepository
{
    Task<IReadOnlyList<EdiFileTypeConfig>> GetAllActiveAsync(CancellationToken ct);
    Task<EdiFileTypeConfig?> GetByCodeAsync(string fileTypeCode, CancellationToken ct);
}

