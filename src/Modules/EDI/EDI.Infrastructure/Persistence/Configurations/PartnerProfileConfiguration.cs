using EDI.Domain.Entities;
using EDI.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public class PartnerProfileConfiguration : IEntityTypeConfiguration<PartnerProfile>
{
    public void Configure(EntityTypeBuilder<PartnerProfile> builder)
    {
        builder.ToTable("PartnerProfiles");

        builder.HasKey(x => x.PartnerCode);

        builder.Property(x => x.PartnerCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

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

        builder.Property(x => x.InboxPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ProcessingPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ArchivePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ErrorPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasData(
            new PartnerProfile(
                "SAMPLE01",
                "Sample Partner 01",
                EdiFormat.Csv,
                EdiSchemaVersion.Create("1.0"),
                "/data/edi/SAMPLE01/inbox",
                "/data/edi/SAMPLE01/processing",
                "/data/edi/SAMPLE01/archive",
                "/data/edi/SAMPLE01/error"
            )
        );
    }
}
