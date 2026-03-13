using FactoryERP.Abstractions.Cqrs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions;

namespace Notification.Application.Features.GetUnreadCount;

public sealed class GetUnreadCountHandler : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    private readonly INotificationDbContext _db;

    public GetUnreadCountHandler(INotificationDbContext db) => _db = db;

    public async Task<Result<int>> Handle(
        GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _db.UserNotifications
            .AsNoTracking()
            .CountAsync(un => un.UserId == request.UserId && !un.IsRead, cancellationToken);

        return Result.Success(count);
    }
}

