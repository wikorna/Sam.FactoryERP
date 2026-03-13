namespace Labeling.Domain.Entities;

/// <summary>
/// User specific override. Can be used to grant access to a printer
/// outside of department/store scope, or explicitly deny access.
/// </summary>
public class UserPrinterOverride
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PrinterId { get; private set; }
    public PrinterAccessType Access { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Printer? Printer { get; private set; }

    private UserPrinterOverride() { }

    public static UserPrinterOverride Allow(Guid userId, Guid printerId, string? reason = null)
    {
        return new UserPrinterOverride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PrinterId = printerId,
            Access = PrinterAccessType.Allow,
            Reason = reason,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static UserPrinterOverride Deny(Guid userId, Guid printerId, string? reason = null)
    {
        return new UserPrinterOverride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PrinterId = printerId,
            Access = PrinterAccessType.Deny,
            Reason = reason,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

