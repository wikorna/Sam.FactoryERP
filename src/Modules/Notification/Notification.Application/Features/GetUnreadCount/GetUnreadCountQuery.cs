using FactoryERP.Abstractions.Cqrs;

namespace Notification.Application.Features.GetUnreadCount;

public sealed record GetUnreadCountQuery(string UserId) : IQuery<int>;

