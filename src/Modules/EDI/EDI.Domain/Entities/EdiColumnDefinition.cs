namespace EDI.Domain.Entities;

/// <summary>
/// Column definition within an <see cref="EdiFileTypeConfig"/>.
/// Drives config-driven CSV parsing and validation.
/// </summary>
public sealed class EdiColumnDefinition
{
    public Guid Id { get; private set; }
    public Guid FileTypeConfigId { get; private set; }

    /// <summary>Zero-based column ordinal in the CSV.</summary>
    public int Ordinal { get; private set; }

    /// <summary>Logical column name, e.g. "PoNumber", "DueDate".</summary>
    public string ColumnName { get; private set; } = string.Empty;

    /// <summary>Data type hint for validation: String, Integer, Decimal, Date, DateTime.</summary>
    public string DataType { get; private set; } = "String";

    public bool IsRequired { get; private set; }

    public int? MaxLength { get; private set; }

    /// <summary>Optional regex for value validation, e.g. "^\d{4}-\d{2}-\d{2}$" for dates.</summary>
    public string? ValidationRegex { get; private set; }

    /// <summary>Human-readable display name for the preview UI.</summary>
    public string? DisplayLabel { get; private set; }

    // Navigation
    public EdiFileTypeConfig FileTypeConfig { get; private set; } = null!;

    private EdiColumnDefinition() { } // EF

    public static EdiColumnDefinition Create(
        int ordinal,
        string columnName,
        string dataType = "String",
        bool isRequired = false,
        int? maxLength = null,
        string? validationRegex = null,
        string? displayLabel = null)
    {
        return new EdiColumnDefinition
        {
            Id = Guid.NewGuid(),
            Ordinal = ordinal,
            ColumnName = columnName,
            DataType = dataType,
            IsRequired = isRequired,
            MaxLength = maxLength,
            ValidationRegex = validationRegex,
            DisplayLabel = displayLabel ?? columnName
        };
    }
}

