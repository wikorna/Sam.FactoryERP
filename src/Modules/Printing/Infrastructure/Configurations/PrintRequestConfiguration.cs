using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Configurations;

public class PrintRequestConfiguration : IEntityTypeConfiguration<PrintRequest>
{
    public void Configure(EntityTypeBuilder<PrintRequest> builder)
    {
        builder.ToTable("PrintRequests");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.Id)
            .ValueGeneratedOnAdd();

        builder.Property(pr => pr.CreatedUtc)
            .IsRequired();
    }
}

