using System.ComponentModel.DataAnnotations;

namespace EDI.Domain.Entities;

public sealed class PurchaseOrderStagingHeader
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }

    // Derived from S1
    public DateOnly? TransmissionDate { get; set; }
    public TimeOnly? TransmissionTime { get; set; }

    // Derived from S3
    [MaxLength(100)]
    public string? PoFileName { get; set; }

    public int? RecordCount { get; set; }

    [MaxLength(50)]
    public string? SupplierCode { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(100)]
    public string? ContactName { get; set; }

    // Navigation property
    public ICollection<PurchaseOrderStagingDetail> Details { get; set; } = new List<PurchaseOrderStagingDetail>();
}
