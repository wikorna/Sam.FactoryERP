using Notification.Domain.Enums;

namespace Notification.Application.Abstractions;

/// <summary>Request model passed to <see cref="INotificationService.CreateForUsersAsync"/>.</summary>
public sealed class NotificationCreateRequest
{
    public required NotificationCategory Category      { get; init; }
    public required NotificationSeverity Severity      { get; init; }
    public required string               Title         { get; init; }
    public required string               Message       { get; init; }
    public string?                       Route         { get; init; }
    public string?                       PayloadJson   { get; init; }
    public Guid?                         CorrelationId { get; init; }

    /// <summary>
    /// Optional dedup key. When non-null the service will skip creation if a
    /// notification with this key already exists, returning the existing id.
    /// Format convention: "{EventName}:{DomainId}" e.g. "QrPrintFailed:printJobId".
    /// </summary>
    public string? DeduplicationKey  { get; init; }
    public string? SourceEventName   { get; init; }
    public string? SourceModule      { get; init; }

    /// <summary>When true the service also dispatches a toast push after persisting.</summary>
    public bool SendToast            { get; init; }

    /// <summary>When true the service refreshes the badge counter push after persisting.</summary>
    public bool RefreshBadge         { get; init; }
}

/// <summary>Outcome returned from <see cref="INotificationService.CreateForUsersAsync"/>.</summary>
public sealed class NotificationCreationResult
{
    /// <summary>True when the notification was newly inserted; false when the dedup key matched.</summary>
    public bool          Created         { get; init; }
    public Guid          NotificationId  { get; init; }

    /// <summary>Ids of <c>UserNotification</c> rows that were created or already existed.</summary>
    public IReadOnlyList<Guid> UserNotificationIds { get; init; } = [];
}

