// ...existing code...
using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Persistence.Configurations;

public sealed class PrinterImageConfiguration : IEntityTypeConfiguration<PrinterImage>
{
    public void Configure(EntityTypeBuilder<PrinterImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StoredAs)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Checksum)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasOne(x => x.Printer)
            .WithMany()
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PrinterId, x.ImageName })
            .IsUnique();
    }
}

