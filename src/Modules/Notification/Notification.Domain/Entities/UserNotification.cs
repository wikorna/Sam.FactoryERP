namespace Notification.Domain.Entities;

/// <summary>
/// Represents the delivery of an <see cref="AppNotification"/> to one specific user.
/// This is the row the user sees in their inbox.
/// </summary>
public sealed class UserNotification
{
    public Guid     Id                     { get; private set; }
    public Guid     NotificationId         { get; private set; }

    /// <summary>
    /// String user identifier matching the value returned by
    /// <c>NotificationUserIdProvider.GetUserId()</c> (the JWT <c>sub</c> / <c>NameIdentifier</c> claim).
    /// </summary>
    public string   UserId                 { get; private set; } = string.Empty;
    public bool     IsRead                 { get; private set; }
    public DateTime? ReadUtc               { get; private set; }

    /// <summary>Set when the realtime SignalR push was confirmed dispatched.</summary>
    public DateTime? DeliveredRealtimeUtc  { get; private set; }
    public DateTime  CreatedUtc            { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public AppNotification? Notification   { get; private set; }

    // ── EF Core ctor ──────────────────────────────────────────────────────────
    private UserNotification() { }

    internal static UserNotification Create(Guid notificationId, string userId)
        => new()
        {
            Id             = Guid.NewGuid(),
            NotificationId = notificationId,
            UserId         = userId,
            IsRead         = false,
            CreatedUtc     = DateTime.UtcNow,
        };

    /// <summary>Marks this delivery as read by the user.  Idempotent.</summary>
    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead  = true;
        ReadUtc = DateTime.UtcNow;
    }

    /// <summary>Records the UTC time a realtime push was dispatched.  Idempotent.</summary>
    public void MarkDeliveredRealtime()
    {
        DeliveredRealtimeUtc ??= DateTime.UtcNow;
    }
}

