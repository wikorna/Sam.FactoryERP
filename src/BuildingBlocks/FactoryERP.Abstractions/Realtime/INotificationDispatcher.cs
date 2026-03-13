namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// Application-level abstraction for pushing real-time notifications to Angular clients.
/// Implementations decouple consumers and domain services from the concrete SignalR hub.
/// </summary>
/// <remarks>
/// Production implementation (<c>NotificationDispatcher</c>) lives in ApiHost and uses
/// <c>IHubContext&lt;NotificationHub, INotificationClient&gt;</c>.
/// WorkerHost uses <c>NullNotificationDispatcher</c> — a no-op implementation that logs
/// the intent without blocking, since the hub runs only in ApiHost.
/// </remarks>
public interface INotificationDispatcher
{
    /// <summary>Pushes a domain-event notification to a single authenticated user.</summary>
    Task NotifyUserAsync(
        string userId,
        string eventType,
        object payload,
        CancellationToken ct = default);

    /// <summary>Pushes a notification to all users who belong to the specified role group.</summary>
    Task NotifyRoleAsync(
        string role,
        string eventType,
        object payload,
        CancellationToken ct = default);

    /// <summary>Broadcasts a notification to every connected client.</summary>
    Task BroadcastAsync(
        string eventType,
        object payload,
        CancellationToken ct = default);

    /// <summary>Sends a transient toast message to a single user.</summary>
    Task ToastUserAsync(
        string userId,
        ToastMessage toast,
        CancellationToken ct = default);
}

