using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public sealed class EdiStagingRowConfiguration : IEntityTypeConfiguration<EdiStagingRow>
{
    public void Configure(EntityTypeBuilder<EdiStagingRow> builder)
    {
        builder.ToTable("EdiStagingRows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileTypeCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.RawLine)
            .IsRequired();

        builder.Property(x => x.ParsedColumnsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.ValidationErrorsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.JobId);
        builder.HasIndex(x => new { x.JobId, x.RowIndex }).IsUnique();
        builder.HasIndex(x => new { x.JobId, x.IsSelected, x.IsValid });
    }
}

