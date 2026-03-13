using FactoryERP.Abstractions.Cqrs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions;
using Notification.Application.DTOs;

namespace Notification.Application.Features.GetNotifications;

public sealed class GetNotificationsHandler
    : IRequestHandler<GetNotificationsQuery, Result<IReadOnlyList<NotificationListItemDto>>>
{
    private readonly INotificationDbContext _db;

    public GetNotificationsHandler(INotificationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<NotificationListItemDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        var skip = Math.Max(0, request.Skip);

        var items = await _db.UserNotifications
            .AsNoTracking()
            .Where(un => un.UserId == request.UserId)
            .OrderByDescending(un => un.CreatedUtc)
            .Skip(skip)
            .Take(take)
            .Select(un => new NotificationListItemDto
            {
                Id          = un.Id,
                Category    = un.Notification!.Category.ToString(),
                Severity    = un.Notification!.Severity.ToString(),
                Title       = un.Notification!.Title,
                Message     = un.Notification!.Message,
                IsRead      = un.IsRead,
                CreatedUtc  = un.CreatedUtc,
                Route       = un.Notification!.Route,
                PayloadJson = un.Notification!.PayloadJson,
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NotificationListItemDto>>(items);
    }
}

