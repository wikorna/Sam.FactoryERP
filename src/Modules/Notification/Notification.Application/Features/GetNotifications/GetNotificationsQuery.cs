using FactoryERP.Abstractions.Cqrs;
using Notification.Application.DTOs;

namespace Notification.Application.Features.GetNotifications;

/// <summary>Returns a paginated list of notifications for the current user.</summary>
public sealed record GetNotificationsQuery(
    string UserId,
    int    Skip = 0,
    int    Take = 20) : IQuery<IReadOnlyList<NotificationListItemDto>>;

