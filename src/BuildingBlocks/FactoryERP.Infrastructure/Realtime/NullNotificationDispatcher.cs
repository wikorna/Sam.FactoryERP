using FactoryERP.Abstractions.Realtime;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Infrastructure.Realtime;

/// <summary>
/// No-op implementation of <see cref="INotificationDispatcher"/> used by the
/// <c>WorkerHost</c> and any host that does not run a SignalR hub.
/// All calls are logged at <c>Debug</c> level and completed synchronously so
/// callers can depend on <c>INotificationDispatcher</c> without branching.
/// </summary>
public sealed class NullNotificationDispatcher : INotificationDispatcher
{
    private readonly ILogger<NullNotificationDispatcher> _logger;

    public NullNotificationDispatcher(ILogger<NullNotificationDispatcher> logger)
        => _logger = logger;

    /// <inheritdoc />
    public Task NotifyUserAsync(
        string userId, string eventType, object payload, CancellationToken ct = default)
    {
        LogUser(userId, eventType);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NotifyRoleAsync(
        string role, string eventType, object payload, CancellationToken ct = default)
    {
        LogRole(role, eventType);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task BroadcastAsync(string eventType, object payload, CancellationToken ct = default)
    {
        LogBroadcast(eventType);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ToastUserAsync(string userId, ToastMessage toast, CancellationToken ct = default)
    {
        LogToast(userId, toast.Level);
        return Task.CompletedTask;
    }

    // ── Analyzer-compliant log helpers ───────────────────────────────────────

    private void LogUser(string userId, string eventType) => _logger.LogDebug("NullDispatcher: would notify user {UserId} with event {EventType}", userId, eventType);

    private void LogRole(string role, string eventType) => _logger.LogDebug("NullDispatcher: would notify role {Role} with event {EventType}", role, eventType);

    private void LogBroadcast(string eventType) => _logger.LogDebug("NullDispatcher: would broadcast event {EventType}", eventType);

    private void LogToast(string userId, string level) => _logger.LogDebug("NullDispatcher: would send [{Level}] toast to user {UserId}", userId, level);
}

