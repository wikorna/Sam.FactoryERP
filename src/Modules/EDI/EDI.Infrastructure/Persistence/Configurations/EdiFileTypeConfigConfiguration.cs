using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public sealed class EdiFileTypeConfigConfiguration : IEntityTypeConfiguration<EdiFileTypeConfig>
{
    public void Configure(EntityTypeBuilder<EdiFileTypeConfig> builder)
    {
        builder.ToTable("EdiFileTypeConfigs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.FileTypeCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.FilenamePrefixPattern)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Delimiter)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue(",");

        builder.Property(x => x.SchemaVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.MaxFileSizeBytes)
            .HasDefaultValue(52_428_800L);

        builder.HasIndex(x => x.FileTypeCode)
            .IsUnique();

        builder.HasIndex(x => x.IsActive);

        builder.HasMany(x => x.Columns)
            .WithOne(x => x.FileTypeConfig)
            .HasForeignKey(x => x.FileTypeConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed SAP MCP file types: Forecast (F*) and Purchase Order (P*)
        var forecastId = new Guid("a1b2c3d4-0001-0001-0001-000000000001");
        var poId = new Guid("a1b2c3d4-0001-0001-0001-000000000002");

        builder.HasData(
            new
            {
                Id = forecastId,
                FileTypeCode = "SAP_FORECAST",
                DisplayName = "SAP MCP Forecast",
                FilenamePrefixPattern = "^F",
                Delimiter = ",",
                HasHeaderRow = true,
                HeaderLineCount = 1,
                SkipLines = 0,
                SchemaVersion = "1.0",
                IsActive = true,
                DetectionPriority = 10,
                MaxFileSizeBytes = 52_428_800L,
                CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = (DateTime?)null
            },
            new
            {
                Id = poId,
                FileTypeCode = "SAP_PO",
                DisplayName = "SAP MCP Purchase Order",
                FilenamePrefixPattern = "^P",
                Delimiter = ",",
                HasHeaderRow = true,
                HeaderLineCount = 1,
                SkipLines = 0,
                SchemaVersion = "1.0",
                IsActive = true,
                DetectionPriority = 20,
                MaxFileSizeBytes = 52_428_800L,
                CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = (DateTime?)null
            });
    }
}

