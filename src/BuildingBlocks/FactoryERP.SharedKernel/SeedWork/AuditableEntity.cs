namespace FactoryERP.SharedKernel.SeedWork;

/// <summary>
/// Entity with audit fields. Use for all business entities that need
/// created/modified tracking.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
}
