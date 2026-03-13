using FactoryERP.Abstractions.Cqrs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions;

namespace Notification.Application.Features.MarkAsRead;

public sealed class MarkNotificationAsReadHandler
    : IRequestHandler<MarkNotificationAsReadCommand, Result>
{
    private readonly INotificationDbContext _db;

    public MarkNotificationAsReadHandler(INotificationDbContext db) => _db = db;

    public async Task<Result> Handle(
        MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var delivery = await _db.UserNotifications
            .FirstOrDefaultAsync(
                un => un.Id == request.UserNotificationId && un.UserId == request.UserId,
                cancellationToken);

        if (delivery is null)
            return Result.Failure(AppError.NotFound(nameof(delivery), request.UserNotificationId));

        delivery.MarkAsRead();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

