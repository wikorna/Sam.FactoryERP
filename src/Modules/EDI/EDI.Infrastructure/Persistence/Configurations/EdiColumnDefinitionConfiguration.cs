using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EDI.Infrastructure.Persistence.Configurations;

public sealed class EdiColumnDefinitionConfiguration : IEntityTypeConfiguration<EdiColumnDefinition>
{
    public void Configure(EntityTypeBuilder<EdiColumnDefinition> builder)
    {
        builder.ToTable("EdiColumnDefinitions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ColumnName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DataType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("String");

        builder.Property(x => x.ValidationRegex)
            .HasMaxLength(500);

        builder.Property(x => x.DisplayLabel)
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.FileTypeConfigId, x.Ordinal })
            .IsUnique();

        // Seed columns for SAP_FORECAST
        var forecastId = new Guid("a1b2c3d4-0001-0001-0001-000000000001");
        builder.HasData(
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000001"), FileTypeConfigId = forecastId, Ordinal = 0, ColumnName = "ForecastId", DataType = "String", IsRequired = true, MaxLength = (int?)50, ValidationRegex = (string?)null, DisplayLabel = "Forecast ID" },
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000002"), FileTypeConfigId = forecastId, Ordinal = 1, ColumnName = "ItemCode", DataType = "String", IsRequired = true, MaxLength = (int?)50, ValidationRegex = (string?)null, DisplayLabel = "Item Code" },
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000003"), FileTypeConfigId = forecastId, Ordinal = 2, ColumnName = "Description", DataType = "String", IsRequired = false, MaxLength = (int?)255, ValidationRegex = (string?)null, DisplayLabel = "Description" },
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000004"), FileTypeConfigId = forecastId, Ordinal = 3, ColumnName = "Quantity", DataType = "Decimal", IsRequired = true, MaxLength = (int?)null, ValidationRegex = (string?)null, DisplayLabel = "Quantity" },
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000005"), FileTypeConfigId = forecastId, Ordinal = 4, ColumnName = "UoM", DataType = "String", IsRequired = false, MaxLength = (int?)20, ValidationRegex = (string?)null, DisplayLabel = "Unit" },
            new { Id = new Guid("b1b2c3d4-0001-0001-0001-000000000006"), FileTypeConfigId = forecastId, Ordinal = 5, ColumnName = "DueDate", DataType = "Date", IsRequired = true, MaxLength = (int?)null, ValidationRegex = (string?)null, DisplayLabel = "Due Date" }
        );

        // Seed columns for SAP_PO
        var poId = new Guid("a1b2c3d4-0001-0001-0001-000000000002");
        builder.HasData(
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000001"), FileTypeConfigId = poId, Ordinal = 0, ColumnName = "PoNumber", DataType = "String", IsRequired = true, MaxLength = (int?)50, ValidationRegex = (string?)null, DisplayLabel = "PO Number" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000002"), FileTypeConfigId = poId, Ordinal = 1, ColumnName = "PoItem", DataType = "String", IsRequired = true, MaxLength = (int?)10, ValidationRegex = (string?)null, DisplayLabel = "PO Item" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000003"), FileTypeConfigId = poId, Ordinal = 2, ColumnName = "ItemCode", DataType = "String", IsRequired = true, MaxLength = (int?)50, ValidationRegex = (string?)null, DisplayLabel = "Item Code" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000004"), FileTypeConfigId = poId, Ordinal = 3, ColumnName = "Description", DataType = "String", IsRequired = false, MaxLength = (int?)255, ValidationRegex = (string?)null, DisplayLabel = "Description" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000005"), FileTypeConfigId = poId, Ordinal = 4, ColumnName = "Quantity", DataType = "Decimal", IsRequired = true, MaxLength = (int?)null, ValidationRegex = (string?)null, DisplayLabel = "Quantity" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000006"), FileTypeConfigId = poId, Ordinal = 5, ColumnName = "UoM", DataType = "String", IsRequired = false, MaxLength = (int?)20, ValidationRegex = (string?)null, DisplayLabel = "Unit" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000007"), FileTypeConfigId = poId, Ordinal = 6, ColumnName = "UnitPrice", DataType = "Decimal", IsRequired = false, MaxLength = (int?)null, ValidationRegex = (string?)null, DisplayLabel = "Unit Price" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000008"), FileTypeConfigId = poId, Ordinal = 7, ColumnName = "DueDate", DataType = "Date", IsRequired = true, MaxLength = (int?)null, ValidationRegex = (string?)null, DisplayLabel = "Due Date" },
            new { Id = new Guid("b1b2c3d4-0002-0001-0001-000000000009"), FileTypeConfigId = poId, Ordinal = 8, ColumnName = "Currency", DataType = "String", IsRequired = false, MaxLength = (int?)10, ValidationRegex = (string?)null, DisplayLabel = "Currency" }
        );
    }
}

