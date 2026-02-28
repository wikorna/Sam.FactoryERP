namespace EDI.Domain.Entities;

/// <summary>
/// Configuration entity for an EDI file type. Adding new SAP MCP file types
/// requires only inserting a row here — no code changes.
/// </summary>
public sealed class EdiFileTypeConfig
{
    public Guid Id { get; private set; }

    /// <summary>Unique code, e.g. "SAP_FORECAST", "SAP_PO".</summary>
    public string FileTypeCode { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Regex matched against the filename, e.g. "^F" for Forecast.</summary>
    public string FilenamePrefixPattern { get; private set; } = string.Empty;

    /// <summary>Column delimiter character(s), e.g. "," or "|".</summary>
    public string Delimiter { get; private set; } = ",";

    public bool HasHeaderRow { get; private set; } = true;

    /// <summary>Number of header lines to display in preview.</summary>
    public int HeaderLineCount { get; private set; } = 1;

    /// <summary>Lines to skip before data rows (e.g. metadata lines).</summary>
    public int SkipLines { get; private set; }

    public string SchemaVersion { get; private set; } = "1.0";

    public bool IsActive { get; private set; } = true;

    /// <summary>Priority for detection ordering (lower = higher priority).</summary>
    public int DetectionPriority { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Maximum file size in bytes allowed for upload (default 50 MB).</summary>
    public long MaxFileSizeBytes { get; private set; } = 52_428_800;

    // Navigation
    private readonly List<EdiColumnDefinition> _columns = [];
    public IReadOnlyList<EdiColumnDefinition> Columns => _columns.AsReadOnly();

    private EdiFileTypeConfig() { } // EF

    public static EdiFileTypeConfig Create(
        string fileTypeCode,
        string displayName,
        string filenamePrefixPattern,
        string delimiter = ",",
        bool hasHeaderRow = true,
        int headerLineCount = 1,
        int skipLines = 0,
        string schemaVersion = "1.0",
        int detectionPriority = 100,
        long maxFileSizeBytes = 52_428_800)
    {
        return new EdiFileTypeConfig
        {
            Id = Guid.NewGuid(),
            FileTypeCode = fileTypeCode,
            DisplayName = displayName,
            FilenamePrefixPattern = filenamePrefixPattern,
            Delimiter = delimiter,
            HasHeaderRow = hasHeaderRow,
            HeaderLineCount = headerLineCount,
            SkipLines = skipLines,
            SchemaVersion = schemaVersion,
            DetectionPriority = detectionPriority,
            MaxFileSizeBytes = maxFileSizeBytes,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void AddColumn(EdiColumnDefinition column)
    {
        _columns.Add(column);
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

