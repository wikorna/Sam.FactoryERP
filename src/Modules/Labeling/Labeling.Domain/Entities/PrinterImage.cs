namespace Labeling.Domain.Entities;

/// <summary>
/// Represents a graphic/image stored in the user memory of a specific printer.
/// Used for ROHS logos, company logos, etc.
/// </summary>
public class PrinterImage
{
    public Guid Id { get; private set; }
    public Guid PrinterId { get; private set; }
    public string ImageName { get; private set; } = string.Empty; // e.g. "ROHS"
    public string StoredAs { get; private set; } = string.Empty; // e.g. "R:ROHS.GRF"
    public string Checksum { get; private set; } = string.Empty;
    public DateTime UploadedAtUtc { get; private set; }

    // Navigation
    public Printer? Printer { get; private set; }

    private PrinterImage() { }

    public static PrinterImage Create(Guid printerId, string imageName, string storedAs, string checksum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedAs);

        return new PrinterImage
        {
            Id = Guid.NewGuid(),
            PrinterId = printerId,
            ImageName = imageName,
            StoredAs = storedAs,
            Checksum = checksum,
            UploadedAtUtc = DateTime.UtcNow
        };
    }
}

