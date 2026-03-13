using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category)
            .IsRequired();

        builder.Property(x => x.Severity)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Route)
            .HasMaxLength(500);

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(400);

        builder.Property(x => x.SourceEventName)
            .HasMaxLength(200);

        builder.Property(x => x.SourceModule)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.DeduplicationKey)
            .IsUnique()
            .HasFilter("\"DeduplicationKey\" IS NOT NULL")
            .HasDatabaseName("IX_Notifications_DeduplicationKey");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_Notifications_CorrelationId");

        builder.HasIndex(x => x.CreatedUtc)
            .HasDatabaseName("IX_Notifications_CreatedUtc");
    }
}

