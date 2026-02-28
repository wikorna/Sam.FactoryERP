using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Infrastructure.Persistence;

public class ItemMasterStagingRecord
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }

    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Uom { get; set; }
    public string? Category { get; set; }
    public string RawLine { get; set; } = string.Empty;
}
