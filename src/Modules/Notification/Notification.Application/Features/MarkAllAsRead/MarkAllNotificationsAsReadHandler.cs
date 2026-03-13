using FactoryERP.Abstractions.Cqrs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions;

namespace Notification.Application.Features.MarkAllAsRead;

public sealed class MarkAllNotificationsAsReadHandler
    : IRequestHandler<MarkAllNotificationsAsReadCommand, Result>
{
    private readonly INotificationDbContext _db;

    public MarkAllNotificationsAsReadHandler(INotificationDbContext db) => _db = db;

    public async Task<Result> Handle(
        MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var unread = await _db.UserNotifications
            .Where(un => un.UserId == request.UserId && !un.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var delivery in unread)
            delivery.MarkAsRead();

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

