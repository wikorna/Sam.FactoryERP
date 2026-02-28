using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

public sealed class RoleAppAccessConfiguration : IEntityTypeConfiguration<RoleAppAccess>
{
    public void Configure(EntityTypeBuilder<RoleAppAccess> builder)
    {
        builder.ToTable("RoleAppAccess");

        builder.HasKey(ra => new { ra.RoleId, ra.AppId });

        builder.HasOne(ra => ra.Role)
            .WithMany(r => r.RoleAppAccesses)
            .HasForeignKey(ra => ra.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ra => ra.App)
            .WithMany(a => a.RoleAppAccesses)
            .HasForeignKey(ra => ra.AppId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ra => ra.AppId);
    }
}

