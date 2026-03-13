namespace Labeling.Domain.Entities;

/// <summary>
/// Mappings for Department-level printer access.
/// </summary>
public class DepartmentPrinter
{
    public Guid Id { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid PrinterId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }

    public Printer? Printer { get; private set; }

    private DepartmentPrinter() { }

    public static DepartmentPrinter Create(Guid departmentId, Guid printerId)
    {
        return new DepartmentPrinter
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            PrinterId = printerId,
            AssignedAtUtc = DateTime.UtcNow
        };
    }
}

