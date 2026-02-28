namespace EDI.Domain.Entities;

/// <summary>
/// Generic staging row for config-driven EDI parsing.
/// Parsed columns are stored as JSONB for maximum flexibility.
/// </summary>
public sealed class EdiStagingRow
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }

    /// <summary>File type code, e.g. "SAP_FORECAST".</summary>
    public string FileTypeCode { get; set; } = string.Empty;

    /// <summary>1-based row index within the file (excluding header/skip lines).</summary>
    public int RowIndex { get; set; }

    /// <summary>Whether the user has selected this row for import.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Original raw CSV line.</summary>
    public string RawLine { get; set; } = string.Empty;

    /// <summary>Column values parsed according to config, stored as JSONB.</summary>
    public string ParsedColumnsJson { get; set; } = "{}";

    /// <summary>Validation errors as JSONB array, e.g. [{"column":"DueDate","error":"Invalid date format"}].</summary>
    public string? ValidationErrorsJson { get; set; }

    /// <summary>Whether the row passed all validation rules.</summary>
    public bool IsValid { get; set; } = true;
}

