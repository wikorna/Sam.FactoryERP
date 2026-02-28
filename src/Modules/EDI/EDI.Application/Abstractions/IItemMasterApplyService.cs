namespace EDI.Application.Abstractions;

public interface IItemMasterApplyService
{
    Task<int> ApplyAsync(Guid ediJobId, IReadOnlyList<ItemMasterStagingRow> rows, CancellationToken ct);
}
