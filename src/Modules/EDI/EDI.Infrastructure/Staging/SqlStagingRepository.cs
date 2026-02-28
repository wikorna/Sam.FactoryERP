using EDI.Application.Abstractions;
using EDI.Application.DTOs;
using EDI.Domain.Entities;
using EDI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.Staging;

public sealed class SqlStagingRepository(EdiDbContext dbContext) : IStagingRepository
{
    public async Task ClearJobAsync(Guid jobId, CancellationToken ct)
    {
        await dbContext.StagingRecords
            .Where(x => x.JobId == jobId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task InsertItemMasterRowAsync(Guid jobId, ItemMasterStagingRow row, CancellationToken ct)
    {
        var record = new ItemMasterStagingRecord
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ItemCode = row.ItemCode,
            ItemName = row.ItemName,
            Uom = row.Uom,
            Category = row.Category,
            RawLine = row.RawLine
        };

        dbContext.StagingRecords.Add(record);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ItemMasterStagingRow>> GetItemMasterRowsAsync(Guid jobId, CancellationToken ct)
    {
        return await dbContext.StagingRecords
            .Where(x => x.JobId == jobId)
            .Select(x => new ItemMasterStagingRow(x.ItemCode, x.ItemName, x.Uom, x.Category, x.RawLine))
            .ToListAsync(ct);
    }

    public async Task InsertPurchaseOrderAsync(Guid jobId, PurchaseOrderDto po, CancellationToken ct)
    {
        var header = new PurchaseOrderStagingHeader
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            TransmissionDate = po.Header.TransmissionDate,
            TransmissionTime = po.Header.TransmissionTime,
            PoFileName = po.Header.PoFileName,
            RecordCount = po.Header.RecordCount,
            SupplierCode = po.Header.SupplierCode,
            SupplierName = po.Header.SupplierName,
            ContactName = po.Header.ContactName
        };

        foreach (var detail in po.Details)
        {
            var detailEntity = new PurchaseOrderStagingDetail
            {
                Id = Guid.NewGuid(),
                HeaderId = header.Id,
                PoStatus = detail.PoStatus,
                PoNumber = detail.PoNumber,
                PoItem = detail.PoItem,
                ItemNo = detail.ItemNo,
                Description = detail.Description,
                BoiName = detail.BoiName,
                DueQty = detail.DueQty,
                Um = detail.Um,
                DueDate = detail.DueDate,
                UnitPrice = detail.UnitPrice,
                Amount = detail.Amount,
                Currency = detail.Currency,
                RawLine = detail.RawLine,
                Header = header
            };
            header.Details.Add(detailEntity);
        }

        dbContext.PurchaseOrderStagingHeaders.Add(header);
        await dbContext.SaveChangesAsync(ct);
    }

    // ── Generic staging rows (config-driven) ──

    public async Task InsertStagingRowsAsync(IReadOnlyList<EdiStagingRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        dbContext.EdiStagingRows.AddRange(rows);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EdiStagingRow>> GetStagingRowsAsync(
        Guid jobId, int pageNumber, int pageSize, CancellationToken ct)
    {
        return await dbContext.EdiStagingRows
            .Where(x => x.JobId == jobId)
            .OrderBy(x => x.RowIndex)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetStagingRowCountAsync(Guid jobId, CancellationToken ct)
    {
        return await dbContext.EdiStagingRows
            .CountAsync(x => x.JobId == jobId, ct);
    }

    public async Task UpdateRowSelectionAsync(
        Guid jobId, IReadOnlyList<int> rowIndexes, bool isSelected, CancellationToken ct)
    {
        await dbContext.EdiStagingRows
            .Where(x => x.JobId == jobId && rowIndexes.Contains(x.RowIndex))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsSelected, isSelected), ct);
    }

    public async Task UpdateRowValidationAsync(EdiStagingRow row, CancellationToken ct)
    {
        dbContext.EdiStagingRows.Update(row);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EdiStagingRow>> GetSelectedValidRowsAsync(
        Guid jobId, CancellationToken ct)
    {
        return await dbContext.EdiStagingRows
            .Where(x => x.JobId == jobId && x.IsSelected && x.IsValid)
            .OrderBy(x => x.RowIndex)
            .ToListAsync(ct);
    }

    public async Task ClearStagingRowsAsync(Guid jobId, CancellationToken ct)
    {
        await dbContext.EdiStagingRows
            .Where(x => x.JobId == jobId)
            .ExecuteDeleteAsync(ct);
    }
}
