using FactoryERP.Abstractions.Cqrs;

namespace Notification.Application.Features.MarkAllAsRead;

public sealed record MarkAllNotificationsAsReadCommand(string UserId) : ICommand;

