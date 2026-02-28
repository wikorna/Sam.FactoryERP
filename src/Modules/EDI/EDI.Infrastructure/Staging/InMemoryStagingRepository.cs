using System.Collections.Concurrent;
using EDI.Application.Abstractions;
using EDI.Application.DTOs;
using EDI.Domain.Entities;

namespace EDI.Infrastructure.Staging;

public sealed class InMemoryStagingRepository : IStagingRepository
{
    private readonly ConcurrentDictionary<Guid, List<ItemMasterStagingRow>> _store = new();
    private readonly ConcurrentDictionary<Guid, List<EdiStagingRow>> _genericStore = new();

    public Task ClearJobAsync(Guid jobId, CancellationToken ct)
    {
        _store[jobId] = new List<ItemMasterStagingRow>();
        return Task.CompletedTask;
    }

    public Task InsertItemMasterRowAsync(Guid jobId, ItemMasterStagingRow row, CancellationToken ct)
    {
        List<ItemMasterStagingRow> list = _store.GetOrAdd(jobId, _ => new List<ItemMasterStagingRow>());
        list.Add(row);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ItemMasterStagingRow>> GetItemMasterRowsAsync(Guid jobId, CancellationToken ct)
    {
        _store.TryGetValue(jobId, out List<ItemMasterStagingRow>? list);
        IReadOnlyList<ItemMasterStagingRow> result = list ?? new List<ItemMasterStagingRow>();
        return Task.FromResult(result);
    }

    public Task InsertPurchaseOrderAsync(Guid jobId, PurchaseOrderDto po, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    // ── Generic staging rows ──

    public Task InsertStagingRowsAsync(IReadOnlyList<EdiStagingRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return Task.CompletedTask;
        var list = _genericStore.GetOrAdd(rows[0].JobId, _ => new List<EdiStagingRow>());
        list.AddRange(rows);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EdiStagingRow>> GetStagingRowsAsync(
        Guid jobId, int pageNumber, int pageSize, CancellationToken ct)
    {
        _genericStore.TryGetValue(jobId, out var list);
        IReadOnlyList<EdiStagingRow> result = (list ?? [])
            .OrderBy(r => r.RowIndex)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> GetStagingRowCountAsync(Guid jobId, CancellationToken ct)
    {
        _genericStore.TryGetValue(jobId, out var list);
        return Task.FromResult(list?.Count ?? 0);
    }

    public Task UpdateRowSelectionAsync(
        Guid jobId, IReadOnlyList<int> rowIndexes, bool isSelected, CancellationToken ct)
    {
        if (_genericStore.TryGetValue(jobId, out var list))
        {
            foreach (var row in list.Where(r => rowIndexes.Contains(r.RowIndex)))
                row.IsSelected = isSelected;
        }
        return Task.CompletedTask;
    }

    public Task UpdateRowValidationAsync(EdiStagingRow row, CancellationToken ct)
    {
        return Task.CompletedTask; // In-memory, already mutated
    }

    public Task<IReadOnlyList<EdiStagingRow>> GetSelectedValidRowsAsync(
        Guid jobId, CancellationToken ct)
    {
        _genericStore.TryGetValue(jobId, out var list);
        IReadOnlyList<EdiStagingRow> result = (list ?? [])
            .Where(r => r.IsSelected && r.IsValid)
            .OrderBy(r => r.RowIndex)
            .ToList();
        return Task.FromResult(result);
    }

    public Task ClearStagingRowsAsync(Guid jobId, CancellationToken ct)
    {
        _genericStore.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }
}
