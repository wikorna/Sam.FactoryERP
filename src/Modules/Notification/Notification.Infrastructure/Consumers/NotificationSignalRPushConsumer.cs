using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="NotificationCreatedIntegrationEvent"/> published by
/// <see cref="Services.NotificationService"/> (in WorkerHost context) and pushes a
/// realtime SignalR event to the target user's Angular client.
///
/// This consumer runs in <b>ApiHost only</b> — it gets the real
/// <c>NotificationDispatcher</c> backed by <c>IHubContext</c>.
/// </summary>
public sealed class NotificationSignalRPushConsumer
    : IConsumer<NotificationCreatedIntegrationEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationSignalRPushConsumer> _logger;

    public NotificationSignalRPushConsumer(
        INotificationDispatcher dispatcher,
        ILogger<NotificationSignalRPushConsumer> logger)
    {
        _dispatcher = dispatcher;
        _logger     = logger;
    }

    public async Task Consume(ConsumeContext<NotificationCreatedIntegrationEvent> context)
    {
        var msg = context.Message;

        if (string.IsNullOrWhiteSpace(msg.TargetUserId))
        {
            LogNoTarget(msg.UserNotificationId);
            return;
        }

        LogPushing(msg.TargetUserId, msg.Category, msg.Severity);

        // ── 1. Deliver notification to user's inbox panel ──────────────────
        await _dispatcher.NotifyUserAsync(
            msg.TargetUserId,
            $"Notification.{msg.Category}",
            new
            {
                id         = msg.UserNotificationId,
                category   = msg.Category,
                severity   = msg.Severity,
                title      = msg.Title,
                message    = msg.Message,
                route      = msg.Route,
                createdUtc = msg.OccurredAtUtc,
                isRead     = false,
            },
            context.CancellationToken);

        // ── 2. Toast ───────────────────────────────────────────────────────
        await _dispatcher.ToastUserAsync(
            msg.TargetUserId,
            new ToastMessage(
                Level: MapSeverityToLevel(msg.Severity),
                Title: msg.Title,
                Body:  msg.Message,
                DurationMs: msg.Severity == "Error" ? 8_000 : 5_000),
            context.CancellationToken);

        LogPushed(msg.TargetUserId);
    }

    private static string MapSeverityToLevel(string severity) => severity switch
    {
        "Success" => "success",
        "Warning" => "warning",
        "Error"   => "error",
        _         => "info",
    };

    private void LogPushing(string userId, string category, string severity) => _logger.LogInformation("Pushing notification to user {UserId}: category={Category}, severity={Severity}", userId, category, severity);

    private void LogPushed(string userId) => _logger.LogInformation("SignalR push dispatched to user {UserId}", userId);

    private void LogNoTarget(Guid userNotificationId) => _logger.LogDebug("NotificationCreatedIntegrationEvent {UserNotificationId} has no TargetUserId — skipping push", userNotificationId);
}

