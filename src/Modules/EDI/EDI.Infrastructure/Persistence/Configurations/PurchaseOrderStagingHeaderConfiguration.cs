using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderStagingHeaderConfiguration : IEntityTypeConfiguration<PurchaseOrderStagingHeader>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderStagingHeader> builder)
    {
        builder.ToTable("PurchaseOrderStagingHeaders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.HasMany(x => x.Details)
            .WithOne(x => x.Header)
            .HasForeignKey(x => x.HeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
