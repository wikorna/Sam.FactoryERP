using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EDI.Domain.Entities;

public sealed class PurchaseOrderStagingDetail
{
    public Guid Id { get; set; }
    public Guid HeaderId { get; set; }

    // Derived from D1
    [MaxLength(50)]
    public string? PoStatus { get; set; }

    [MaxLength(50)]
    public string? PoNumber { get; set; }

    [MaxLength(10)]
    public string? PoItem { get; set; }

    [MaxLength(50)]
    public string? ItemNo { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(255)]
    public string? BoiName { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? DueQty { get; set; }

    [MaxLength(20)]
    public string? Um { get; set; }

    public DateOnly? DueDate { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? UnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Amount { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    // Other fields from D1 not explicitly mapped but part of the raw line
    // Type of Pay, Production, Delivery, Ship To, etc. 
    // We can add them as needed or store the RawLine if we want to be safe.

    public string? RawLine { get; set; }

    public PurchaseOrderStagingHeader Header { get; set; } = null!;
}
