namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// Strongly-typed contract that defines what the server can invoke on
/// connected Angular SignalR clients.
/// Used as the <c>TClient</c> generic parameter on <c>Hub&lt;TClient&gt;</c>.
/// </summary>
public interface INotificationClient
{
    /// <summary>Delivers a domain event notification to the client.</summary>
    Task ReceiveNotification(NotificationMessage message, CancellationToken ct = default);

    /// <summary>Delivers a UI toast/snackbar message to the client.</summary>
    Task ReceiveToast(ToastMessage message, CancellationToken ct = default);

    /// <summary>
    /// Instructs the client to refresh its unread-notification badge counter
    /// without sending full notification data.
    /// </summary>
    Task RefreshNotificationBadge(int unreadCount, CancellationToken ct = default);
}

