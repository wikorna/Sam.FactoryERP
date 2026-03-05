using EDI.Domain.Enums;

namespace EDI.Domain.Entities;

public class EdiStagingFileError
{
    public Guid Id { get; set; }
    public Guid StagingFileId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RowNumber { get; set; }
    public string? ColumnName { get; set; }
    public EdiSeverity Severity { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    
    public EdiStagingFile? StagingFile { get; set; }
}
