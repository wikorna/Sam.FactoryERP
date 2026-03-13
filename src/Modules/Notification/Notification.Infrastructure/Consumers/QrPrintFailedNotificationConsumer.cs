using FactoryERP.Contracts.Labeling;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="QrPrintFailedIntegrationEvent"/> published by
/// <c>PrintZplCommandConsumer</c> / <c>QrPrintRequestedConsumer</c> and creates
/// a persistent notification for the affected user(s).
/// </summary>
/// <remarks>
/// Idempotency: the dedup key format is <c>QrPrintFailed:{PrintJobId}:{UserId}</c>.
/// On MassTransit redelivery the service silently returns the existing notification.
/// </remarks>
public sealed partial class QrPrintFailedNotificationConsumer
    : IConsumer<QrPrintFailedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<QrPrintFailedNotificationConsumer> _logger;

    public QrPrintFailedNotificationConsumer(
        INotificationService notificationService,
        ILogger<QrPrintFailedNotificationConsumer> logger)
    {
        _notificationService = notificationService;
        _logger              = logger;
    }

    public async Task Consume(ConsumeContext<QrPrintFailedIntegrationEvent> context)
    {
        var msg = context.Message;
        LogConsuming(msg.PrintJobId, msg.CorrelationId);

        // ── Resolve recipient(s) ──────────────────────────────────────────
        // Phase 1: use RequesterUserId if available (Guid string from ICurrentUserService).
        // Phase 2: extend with role-based lookup (WarehouseSupervisor) when user directory
        // is accessible from this module without coupling.
        var recipients = new List<string>();

        if (!string.IsNullOrWhiteSpace(msg.RequesterUserId))
            recipients.Add(msg.RequesterUserId);
        else if (!string.IsNullOrWhiteSpace(msg.RequestedBy))
            recipients.Add(msg.RequestedBy); // fallback: username string

        if (recipients.Count == 0)
        {
            LogNoRecipients(msg.PrintJobId);
            return;
        }

        var printerLabel = msg.PrinterName ?? msg.PrinterId.ToString();
        var route        = $"/labeling/print-jobs/{msg.PrintJobId}";

        foreach (var userId in recipients)
        {
            var dedupKey = $"QrPrintFailed:{msg.PrintJobId}:{userId}";

            var request = new NotificationCreateRequest
            {
                Category        = NotificationCategory.Printing,
                Severity        = NotificationSeverity.Error,
                Title           = "QR Tag printing failed",
                Message         = $"Print job {msg.PrintJobId} failed on printer {printerLabel}. {msg.FailureReason}",
                Route           = route,
                CorrelationId   = msg.CorrelationId,
                DeduplicationKey = dedupKey,
                SourceEventName = nameof(QrPrintFailedIntegrationEvent),
                SourceModule    = "Labeling",
                SendToast       = true,
                RefreshBadge    = true,
            };

            await _notificationService.CreateForUsersAsync(
                request, [userId], context.CancellationToken);
        }

        LogProcessed(msg.PrintJobId, recipients.Count);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Consuming QrPrintFailedIntegrationEvent: PrintJobId={PrintJobId}, CorrelationId={CorrelationId}")]
    private partial void LogConsuming(Guid printJobId, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "QrPrintFailedIntegrationEvent for PrintJobId={PrintJobId} has no resolvable recipients — skipping notification")]
    private partial void LogNoRecipients(Guid printJobId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "QrPrintFailed notification created for PrintJobId={PrintJobId}, recipients={RecipientCount}")]
    private partial void LogProcessed(Guid printJobId, int recipientCount);
}

