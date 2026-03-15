using FactoryERP.Abstractions.Realtime;
using FactoryERP.ApiHost.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FactoryERP.ApiHost.Infrastructure.Realtime;

/// <summary>
/// Production implementation of <see cref="INotificationDispatcher"/> that
/// delegates to <see cref="IHubContext{THub,TClient}"/> to push messages to
/// connected Angular SignalR clients.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b> in ApiHost's DI container so it shares the
/// lifetime of the HTTP request (or message-consumer scope).
/// </remarks>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hub;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IHubContext<NotificationHub, INotificationClient> hub,
        ILogger<NotificationDispatcher> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyUserAsync(
        string userId, string eventType, object payload, CancellationToken ct = default)
    {
        var message = new NotificationMessage(eventType, payload, DateTimeOffset.UtcNow);
        await _hub.Clients.User(userId).ReceiveNotification(message, ct);
        LogNotifyUser(userId, eventType);
    }

    /// <inheritdoc />
    public async Task NotifyRoleAsync(
        string role, string eventType, object payload, CancellationToken ct = default)
    {
        var message = new NotificationMessage(eventType, payload, DateTimeOffset.UtcNow);
        // Groups are populated on connect in NotificationHub.OnConnectedAsync()
        await _hub.Clients.Group($"role:{role}").ReceiveNotification(message, ct);
        LogNotifyRole(role, eventType);
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(
        string eventType, object payload, CancellationToken ct = default)
    {
        var message = new NotificationMessage(eventType, payload, DateTimeOffset.UtcNow);
        await _hub.Clients.All.ReceiveNotification(message, ct);
        LogBroadcast(eventType);
    }

    /// <inheritdoc />
    public async Task ToastUserAsync(
        string userId, ToastMessage toast, CancellationToken ct = default)
    {
        await _hub.Clients.User(userId).ReceiveToast(toast, ct);
        LogToast(userId, toast.Level);
    }

    // ── Analyzer-compliant log helpers ───────────────────────────────────────

    private void LogNotifyUser(string userId, string eventType) => _logger.LogInformation("Pushed {EventType} notification to user {UserId}", userId, eventType);

    private void LogNotifyRole(string role, string eventType) => _logger.LogInformation("Pushed {EventType} notification to role group {Role}", role, eventType);

    private void LogBroadcast(string eventType) => _logger.LogInformation("Broadcast {EventType} notification to all clients", eventType);

    private void LogToast(string userId, string toastLevel) => _logger.LogDebug("Sent [{ToastLevel}] toast to user {UserId}", userId, toastLevel);
}

