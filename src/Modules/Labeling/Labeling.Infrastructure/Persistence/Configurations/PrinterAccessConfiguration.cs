using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Persistence.Configurations;

public class PrinterAccessConfiguration :
    IEntityTypeConfiguration<DepartmentPrinter>,
    IEntityTypeConfiguration<StorePrinter>,
    IEntityTypeConfiguration<UserPrinterOverride>
{
    public void Configure(EntityTypeBuilder<DepartmentPrinter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DepartmentId, x.PrinterId }).IsUnique();

        builder.HasOne(x => x.Printer)
            .WithMany()
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<StorePrinter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.StoreId, x.PrinterId }).IsUnique();

        builder.HasOne(x => x.Printer)
            .WithMany()
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<UserPrinterOverride> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Access).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(255);

        builder.HasIndex(x => new { x.UserId, x.PrinterId }).IsUnique();

        builder.HasOne(x => x.Printer)
            .WithMany()
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

