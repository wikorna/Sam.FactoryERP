using FactoryERP.Abstractions.Cqrs;

namespace Notification.Application.Features.MarkAsRead;

/// <param name="UserNotificationId">PK of the <c>UserNotification</c> row to mark.</param>
/// <param name="UserId">Security fence — only the owning user may mark their own rows.</param>
public sealed record MarkNotificationAsReadCommand(
    Guid   UserNotificationId,
    string UserId) : ICommand;

