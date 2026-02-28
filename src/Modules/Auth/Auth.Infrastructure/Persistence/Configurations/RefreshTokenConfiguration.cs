using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="RefreshToken"/>.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.IsRevoked, e.ExpiresAtUtc });
        builder.HasIndex(e => e.Family);
        builder.Property(e => e.CreatedByIp).HasMaxLength(45);
        builder.Property(e => e.UserAgentHash).HasMaxLength(128);
        builder.Property(e => e.RevokedReason).HasMaxLength(256);
    }
}
