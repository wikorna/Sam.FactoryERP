using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

public sealed class AppDefinitionConfiguration : IEntityTypeConfiguration<AppDefinition>
{
    public void Configure(EntityTypeBuilder<AppDefinition> builder)
    {
        builder.ToTable("Apps");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(a => a.Key)
            .IsUnique();

        builder.Property(a => a.Title)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.Route)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.IconCssClass)
            .HasMaxLength(128);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(a => a.RoleAppAccesses)
            .WithOne(ra => ra.App)
            .HasForeignKey(ra => ra.AppId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

