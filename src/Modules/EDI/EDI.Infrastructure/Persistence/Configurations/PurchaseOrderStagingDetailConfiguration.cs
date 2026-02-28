using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderStagingDetailConfiguration : IEntityTypeConfiguration<PurchaseOrderStagingDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderStagingDetail> builder)
    {
        builder.ToTable("PurchaseOrderStagingDetails");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}
