using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public class ItemMasterStagingRecordConfiguration : IEntityTypeConfiguration<ItemMasterStagingRecord>
{
    public void Configure(EntityTypeBuilder<ItemMasterStagingRecord> builder)
    {
        builder.ToTable("Staging_ItemMaster");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemCode).HasMaxLength(50);
        builder.Property(x => x.ItemName).HasMaxLength(200);
        builder.Property(x => x.Uom).HasMaxLength(20);
        builder.Property(x => x.Category).HasMaxLength(50);

        builder.HasIndex(x => x.JobId);
    }
}
