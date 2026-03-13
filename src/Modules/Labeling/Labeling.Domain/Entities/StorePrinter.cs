namespace Labeling.Domain.Entities;

/// <summary>
/// Mappings for Store/Location-level printer access.
/// </summary>
public class StorePrinter
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid PrinterId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }

    public Printer? Printer { get; private set; }

    private StorePrinter() { }

    public static StorePrinter Create(Guid storeId, Guid printerId)
    {
        return new StorePrinter
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            PrinterId = printerId,
            AssignedAtUtc = DateTime.UtcNow
        };
    }
}

