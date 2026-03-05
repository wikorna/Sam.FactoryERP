using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public class EdiStagingFileErrorConfiguration : IEntityTypeConfiguration<EdiStagingFileError>
{
    public void Configure(EntityTypeBuilder<EdiStagingFileError> builder)
    {
        builder.ToTable("EdiStagingFileErrors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ColumnName).HasMaxLength(100);
    }
}
