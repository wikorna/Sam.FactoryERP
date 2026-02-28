using EDI.Domain.Entities;

namespace EDI.Application.Abstractions;

public interface IStagingRepository
{
    Task ClearJobAsync(Guid jobId, CancellationToken ct);
    Task InsertItemMasterRowAsync(Guid jobId, ItemMasterStagingRow row, CancellationToken ct);
    Task<IReadOnlyList<ItemMasterStagingRow>> GetItemMasterRowsAsync(Guid jobId, CancellationToken ct);
    Task InsertPurchaseOrderAsync(Guid jobId, EDI.Application.DTOs.PurchaseOrderDto po, CancellationToken ct);

    // ── Generic staging rows (config-driven) ──
    Task InsertStagingRowsAsync(IReadOnlyList<EdiStagingRow> rows, CancellationToken ct);
    Task<IReadOnlyList<EdiStagingRow>> GetStagingRowsAsync(Guid jobId, int pageNumber, int pageSize, CancellationToken ct);
    Task<int> GetStagingRowCountAsync(Guid jobId, CancellationToken ct);
    Task UpdateRowSelectionAsync(Guid jobId, IReadOnlyList<int> rowIndexes, bool isSelected, CancellationToken ct);
    Task UpdateRowValidationAsync(EdiStagingRow row, CancellationToken ct);
    Task<IReadOnlyList<EdiStagingRow>> GetSelectedValidRowsAsync(Guid jobId, CancellationToken ct);
    Task ClearStagingRowsAsync(Guid jobId, CancellationToken ct);
}

public sealed record ItemMasterStagingRow(
    string ItemCode,
    string ItemName,
    string? Uom,
    string? Category,
    string RawLine);

