using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="SigningKeyInfo"/>.</summary>
public sealed class SigningKeyInfoConfiguration : IEntityTypeConfiguration<SigningKeyInfo>
{
    public void Configure(EntityTypeBuilder<SigningKeyInfo> builder)
    {
        builder.HasKey(e => e.Kid);
        builder.Property(e => e.Kid).HasMaxLength(64);
    }
}
