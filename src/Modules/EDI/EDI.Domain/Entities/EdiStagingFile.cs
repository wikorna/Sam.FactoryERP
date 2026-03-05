using EDI.Domain.Enums;

namespace EDI.Domain.Entities;

public class EdiStagingFile
{
    public Guid Id { get; set; }
    public string? ClientId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public EdiFileType FileType { get; set; }
    public string SchemaKey { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    
    public EdiStagingStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    
    public int? RowCountTotal { get; set; }
    public int? RowCountProcessed { get; set; }
    
    // JSON properties
    public string? DetectResultJson { get; set; }
    public string? ValidationResultJson { get; set; }
    
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Concurrency Token
    public byte[] RowVersion { get; set; } = [];
    
    // Virtual collection for associated errors
    public ICollection<EdiStagingFileError> Errors { get; set; } = new List<EdiStagingFileError>();
}
