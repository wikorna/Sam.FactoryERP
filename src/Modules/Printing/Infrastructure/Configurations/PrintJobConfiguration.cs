using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Configurations;

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("PrintJobs");

        builder.HasKey(pj => pj.Id);

        builder.Property(pj => pj.Id)
            .ValueGeneratedOnAdd();

        builder.Property(pj => pj.Status)
            .IsRequired();

        builder.Property(pj => pj.CreatedUtc)
            .IsRequired();

        builder.HasOne(pj => pj.Printer)
            .WithMany()
            .HasForeignKey(pj => pj.PrinterId);

        builder.HasOne(pj => pj.PrintRequest)
            .WithMany()
            .HasForeignKey(pj => pj.PrintRequestId);
    }
}

