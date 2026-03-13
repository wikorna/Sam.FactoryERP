namespace FactoryERP.Contracts.Messaging;

/// <summary>
/// Published by <c>NotificationService</c> after persisting a <c>UserNotification</c>.
/// Consumed by <c>NotificationSignalRPushConsumer</c> in ApiHost to push the realtime
/// SignalR event to the target user's connected Angular clients.
/// </summary>
/// <remarks>
/// This is a best-effort delivery event — if SignalR push fails the notification is
/// already persisted in the database and the user will see it on the next inbox poll.
/// </remarks>
public sealed record NotificationCreatedIntegrationEvent : IntegrationEvent
{
    /// <summary>The <c>UserNotification.Id</c> created in the notifications schema.</summary>
    public required Guid UserNotificationId { get; init; }

    /// <summary>
    /// String user ID (Guid.ToString()) used to route via <c>IHubContext.Clients.User()</c>.
    /// Null if the originating event lacked user context — in that case, no push is attempted.
    /// </summary>
    public string? TargetUserId { get; init; }

    /// <summary>Category string for Angular routing decisions.</summary>
    public required string Category { get; init; }

    /// <summary>Severity string: Info | Success | Warning | Error.</summary>
    public required string Severity { get; init; }

    /// <summary>Short notification title.</summary>
    public required string Title { get; init; }

    /// <summary>Full notification body.</summary>
    public required string Message { get; init; }

    /// <summary>Optional Angular deep-link route, e.g. <c>/labeling/print-jobs/123</c>.</summary>
    public string? Route { get; init; }
}

