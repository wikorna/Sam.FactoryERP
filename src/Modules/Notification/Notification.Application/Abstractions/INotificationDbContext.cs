using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

namespace Notification.Application.Abstractions;

public interface INotificationDbContext
{
    DbSet<AppNotification>  Notifications     { get; }
    DbSet<UserNotification> UserNotifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

