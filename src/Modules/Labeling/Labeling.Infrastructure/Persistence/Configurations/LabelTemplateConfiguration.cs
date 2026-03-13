using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="LabelTemplate"/>.</summary>
public sealed class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
{
    public void Configure(EntityTypeBuilder<LabelTemplate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TemplateKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Version)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ZplBody)
            .IsRequired();

        builder.Property(x => x.DesignDpi)
            .IsRequired()
            .HasDefaultValue(300);

        builder.Property(x => x.LabelWidthMm)
            .IsRequired();

        builder.Property(x => x.LabelHeightMm)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        // ── Indexes ───────────────────────────────────────────────────────
        // Only one active version per template key.
        builder.HasIndex(x => new { x.TemplateKey, x.Version })
            .IsUnique()
            .HasDatabaseName("IX_LabelTemplates_Key_Version");

        builder.HasIndex(x => new { x.TemplateKey, x.IsActive })
            .HasDatabaseName("IX_LabelTemplates_Key_Active");
    }
}

