using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions;
using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="INotificationService"/>.
/// Flow: validate → dedup check → persist → publish <see cref="NotificationCreatedIntegrationEvent"/>
/// → (optional) best-effort realtime SignalR push via <see cref="INotificationDispatcher"/>.
/// </summary>
public sealed partial class NotificationService : INotificationService
{
    private readonly INotificationDbContext    _db;
    private readonly IPublishEndpoint          _bus;
    private readonly INotificationDispatcher   _dispatcher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationDbContext    db,
        IPublishEndpoint          bus,
        INotificationDispatcher   dispatcher,
        ILogger<NotificationService> logger)
    {
        _db         = db;
        _bus        = bus;
        _dispatcher = dispatcher;
        _logger     = logger;
    }

    /// <inheritdoc/>
    public async Task<NotificationCreationResult> CreateForUsersAsync(
        NotificationCreateRequest request,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Deduplication check ──────────────────────────────────────────
        if (request.DeduplicationKey is not null)
        {
            var existing = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.DeduplicationKey == request.DeduplicationKey)
                .Select(n => new { n.Id, DeliveryIds = n.Deliveries.Select(d => d.Id).ToList() })
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                LogDuplicateSkipped(request.DeduplicationKey, existing.Id);
                return new NotificationCreationResult
                {
                    Created              = false,
                    NotificationId       = existing.Id,
                    UserNotificationIds  = existing.DeliveryIds,
                };
            }
        }

        // ── 2. Create aggregate + delivery rows ────────────────────────────
        var notification = AppNotification.Create(
            category:        request.Category,
            severity:        request.Severity,
            title:           request.Title,
            message:         request.Message,
            route:           request.Route,
            payloadJson:     request.PayloadJson,
            correlationId:   request.CorrelationId,
            deduplicationKey: request.DeduplicationKey,
            sourceEventName: request.SourceEventName,
            sourceModule:    request.SourceModule);

        var deliveries = userIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(uid => notification.AddDelivery(uid))
            .ToList();

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        LogCreated(notification.Id, deliveries.Count, request.Category);

        // ── 3. Publish integration event for ApiHost SignalR push ──────────
        // Fire-and-forget per user; if publish fails, notification is already in DB.
        foreach (var delivery in deliveries)
        {
            try
            {
                await _bus.Publish(new NotificationCreatedIntegrationEvent
                {
                    CorrelationId       = request.CorrelationId ?? Guid.NewGuid(),
                    TargetUserId        = delivery.UserId,
                    UserNotificationId  = delivery.Id,
                    Category            = request.Category.ToString(),
                    Severity            = request.Severity.ToString(),
                    Title               = request.Title,
                    Message             = request.Message,
                    Route               = request.Route,
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // Bus publish failure must NOT roll back the persisted notification.
                LogBusPublishFailed(delivery.UserId, notification.Id, ex);
            }
        }

        // ── 4. In-process best-effort push (ApiHost only — NullDispatcher in Worker) ──
        // This provides zero-latency push when the notification is created inside ApiHost
        // (e.g., from a command handler). WorkerHost gets NullNotificationDispatcher.
        foreach (var delivery in deliveries)
        {
            try
            {
                var msg = new NotificationMessage(
                    EventType:  $"Notification.{request.Category}",
                    Payload: new
                    {
                        id          = delivery.Id,
                        category    = request.Category.ToString(),
                        severity    = request.Severity.ToString(),
                        title       = request.Title,
                        message     = request.Message,
                        route       = request.Route,
                        createdUtc  = notification.CreatedUtc,
                        isRead      = false,
                    },
                    OccurredAt: DateTimeOffset.UtcNow);

                await _dispatcher.NotifyUserAsync(
                    delivery.UserId,
                    $"Notification.{request.Category}",
                    msg.Payload,
                    cancellationToken);

                if (request.SendToast)
                    await _dispatcher.ToastUserAsync(
                        delivery.UserId,
                        new ToastMessage(
                            Level: MapSeverityToLevel(request.Severity),
                            Title: request.Title,
                            Body:  request.Message),
                        cancellationToken);

                if (request.RefreshBadge)
                {
                    var unread = await _db.UserNotifications
                        .AsNoTracking()
                        .CountAsync(un => un.UserId == delivery.UserId && !un.IsRead, cancellationToken);
                    await _dispatcher.BroadcastAsync("RefreshBadge", new { userId = delivery.UserId, unread }, cancellationToken);
                }

                delivery.MarkDeliveredRealtime();
            }
            catch (Exception ex)
            {
                LogRealtimePushFailed(delivery.UserId, notification.Id, ex);
            }
        }

        // Persist any DeliveredRealtimeUtc stamps set above
        if (deliveries.Any(d => d.DeliveredRealtimeUtc.HasValue))
            await _db.SaveChangesAsync(cancellationToken);

        return new NotificationCreationResult
        {
            Created             = true,
            NotificationId      = notification.Id,
            UserNotificationIds = deliveries.Select(d => d.Id).ToList(),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MapSeverityToLevel(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => "success",
        NotificationSeverity.Warning => "warning",
        NotificationSeverity.Error   => "error",
        _                            => "info",
    };

    // ── Analyzer-compliant log helpers ────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Notification {NotificationId} created for {DeliveryCount} user(s), category={Category}")]
    private partial void LogCreated(Guid notificationId, int deliveryCount, NotificationCategory category);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Notification dedup hit for key '{DeduplicationKey}', existing={ExistingId} — skipping")]
    private partial void LogDuplicateSkipped(string deduplicationKey, Guid existingId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Bus publish failed for user {UserId}, notification {NotificationId} — notification persisted in DB")]
    private partial void LogBusPublishFailed(string userId, Guid notificationId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Realtime push failed for user {UserId}, notification {NotificationId} — inbox delivery unaffected")]
    private partial void LogRealtimePushFailed(string userId, Guid notificationId, Exception ex);
}

