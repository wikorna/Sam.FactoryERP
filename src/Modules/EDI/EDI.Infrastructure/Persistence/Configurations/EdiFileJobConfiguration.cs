using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public class EdiFileJobConfiguration : IEntityTypeConfiguration<EdiFileJob>
{
    public void Configure(EntityTypeBuilder<EdiFileJob> builder)
    {
        builder.ToTable("EdiFileJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.PartnerCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.FileTypeCode)
            .HasMaxLength(50);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.SourcePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Sha256)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Format)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                v => v.Value,
                v => new EdiFormat(v));

        builder.Property(x => x.SchemaVersion)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion(
                v => v.Value,
                v => EdiSchemaVersion.Create(v));

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => new { x.PartnerCode, x.Sha256 })
            .IsUnique();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
