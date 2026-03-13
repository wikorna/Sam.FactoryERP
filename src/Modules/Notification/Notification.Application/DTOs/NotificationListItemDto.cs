namespace Notification.Application.DTOs;

/// <summary>
/// Compact projection returned by GET /api/notifications.
/// Angular-friendly field names; no internal EF navigation types leaked.
/// </summary>
public sealed class NotificationListItemDto
{
    public Guid     Id           { get; init; }
    public string   Category     { get; init; } = string.Empty;
    public string   Severity     { get; init; } = string.Empty;
    public string   Title        { get; init; } = string.Empty;
    public string   Message      { get; init; } = string.Empty;
    public bool     IsRead       { get; init; }
    public DateTime CreatedUtc   { get; init; }
    public string?  Route        { get; init; }
    public string?  PayloadJson  { get; init; }
}

