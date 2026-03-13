using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

/// <summary>
/// The canonical notification record — content is shared across all recipients.
/// One <see cref="AppNotification"/> can be delivered to many users via
/// <see cref="UserNotification"/> rows.
/// </summary>
public sealed class AppNotification
{
    public Guid   Id               { get; private set; }
    public NotificationCategory Category    { get; private set; }
    public NotificationSeverity Severity    { get; private set; }
    public string Title            { get; private set; } = string.Empty;
    public string Message          { get; private set; } = string.Empty;

    /// <summary>Optional Angular deep-link, e.g. <c>/labeling/print-jobs/{id}</c>.</summary>
    public string? Route           { get; private set; }

    /// <summary>Serialised JSON payload for UI rendering (may be null).</summary>
    public string? PayloadJson     { get; private set; }

    /// <summary>End-to-end correlation token from the originating command/event.</summary>
    public Guid?   CorrelationId   { get; private set; }

    /// <summary>
    /// Optional deduplication key.  A unique index on this column prevents
    /// duplicate notifications from the same business event on redelivery.
    /// </summary>
    public string? DeduplicationKey { get; private set; }

    /// <summary>Name of the integration event that triggered this notification.</summary>
    public string? SourceEventName { get; private set; }

    /// <summary>Module that originated the event (e.g. "Labeling", "EDI").</summary>
    public string? SourceModule    { get; private set; }

    /// <summary>UTC timestamp when this notification content was created.</summary>
    public DateTime CreatedUtc     { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    private readonly List<UserNotification> _deliveries = [];
    public IReadOnlyList<UserNotification> Deliveries => _deliveries.AsReadOnly();

    // ── EF Core ctor ──────────────────────────────────────────────────────────
    private AppNotification() { }

    /// <summary>Factory — creates the notification and its user-delivery rows atomically.</summary>
    public static AppNotification Create(
        NotificationCategory category,
        NotificationSeverity severity,
        string title,
        string message,
        string? route              = null,
        string? payloadJson        = null,
        Guid?   correlationId      = null,
        string? deduplicationKey   = null,
        string? sourceEventName    = null,
        string? sourceModule       = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new AppNotification
        {
            Id               = Guid.NewGuid(),
            Category         = category,
            Severity         = severity,
            Title            = title.Trim(),
            Message          = message.Trim(),
            Route            = route,
            PayloadJson      = payloadJson,
            CorrelationId    = correlationId,
            DeduplicationKey = deduplicationKey,
            SourceEventName  = sourceEventName,
            SourceModule     = sourceModule,
            CreatedUtc       = DateTime.UtcNow,
        };
    }

    /// <summary>Adds a delivery row targeting <paramref name="userId"/>.</summary>
    public UserNotification AddDelivery(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var delivery = UserNotification.Create(this.Id, userId);
        _deliveries.Add(delivery);
        return delivery;
    }
}

