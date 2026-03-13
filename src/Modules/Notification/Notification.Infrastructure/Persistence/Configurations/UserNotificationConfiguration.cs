using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        // ── FK to AppNotification ─────────────────────────────────────────────
        builder.HasOne(x => x.Notification)
            .WithMany(n => n.Deliveries)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────────

        // Primary query: all notifications for a user, unread first, newest first
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedUtc })
            .HasDatabaseName("IX_UserNotifications_UserId_IsRead_CreatedUtc");

        // For fast lookup of a specific delivery row
        builder.HasIndex(x => new { x.NotificationId, x.UserId })
            .HasDatabaseName("IX_UserNotifications_NotificationId_UserId");
    }
}

