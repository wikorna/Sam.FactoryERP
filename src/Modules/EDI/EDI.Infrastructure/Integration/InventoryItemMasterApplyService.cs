using EDI.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Integration;

public sealed class InventoryItemMasterApplyService : IItemMasterApplyService
{
    private readonly ILogger<InventoryItemMasterApplyService> _logger;

    public InventoryItemMasterApplyService(ILogger<InventoryItemMasterApplyService> logger)
    {
        _logger = logger;
    }

    public Task<int> ApplyAsync(Guid ediJobId, IReadOnlyList<ItemMasterStagingRow> rows, CancellationToken ct)
    {
        LogApplyingItemMaster(rows.Count, ediJobId);

        // TODO: Call Inventory Module Command here
        // For now, assume success
        return Task.FromResult(rows.Count);
    }

    private void LogApplyingItemMaster(int count, Guid jobId) => _logger.LogInformation("Applying {Count} item master rows from EDI Job {JobId} to Inventory (STUB).", count, jobId);
}
