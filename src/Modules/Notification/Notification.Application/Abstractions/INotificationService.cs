namespace Notification.Application.Abstractions;

/// <summary>
/// Core domain service for creating persistent notifications.
/// Called by RabbitMQ consumers and Application command handlers.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Persists an <c>AppNotification</c> and one <c>UserNotification</c> per user,
    /// enforces deduplication, then (optionally) triggers a realtime SignalR push.
    /// </summary>
    /// <param name="request">Notification content and delivery options.</param>
    /// <param name="userIds">
    /// One or more user identifiers (Guid.ToString() or username string).
    /// Each produces a separate <c>UserNotification</c> row.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NotificationCreationResult"/> describing what was created.
    /// Never throws for deduplication hits — returns the existing ids instead.
    /// </returns>
    Task<NotificationCreationResult> CreateForUsersAsync(
        NotificationCreateRequest request,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}

