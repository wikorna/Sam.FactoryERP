using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="JtiBlacklistEntry"/>.</summary>
public sealed class JtiBlacklistEntryConfiguration : IEntityTypeConfiguration<JtiBlacklistEntry>
{
    public void Configure(EntityTypeBuilder<JtiBlacklistEntry> builder)
    {
        builder.HasKey(e => e.Jti);
        builder.Property(e => e.Jti).HasMaxLength(128);
        builder.HasIndex(e => e.ExpiresAtUtc); // for TTL-based cleanup
    }
}
