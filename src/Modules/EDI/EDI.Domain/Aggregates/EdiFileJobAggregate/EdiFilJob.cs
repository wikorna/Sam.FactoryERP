using EDI.Domain.ValueObjects;
using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Domain.Aggregates.EdiFileJobAggregate;

public sealed class EdiFileJob
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string PartnerCode { get; private set; } = string.Empty;

    /// <summary>Auto-detected file type code, e.g. "SAP_FORECAST", "SAP_PO".</summary>
    public string? FileTypeCode { get; private set; }

    public string FileName { get; private set; } = string.Empty;
    public string SourcePath { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;

    public EdiFormat Format { get; private set; } = EdiFormat.Csv;
    public EdiSchemaVersion SchemaVersion { get; private set; } = EdiSchemaVersion.V1;

    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }

    public EdiFileJobStatus Status { get; private set; } = EdiFileJobStatus.Received;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int ParsedRecords { get; private set; }
    public int AppliedRecords { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private EdiFileJob() { } // for ORM

    public static EdiFileJob CreateReceived(
        Guid id,
        string partnerCode,
        string fileName,
        string sourcePath,
        long sizeBytes,
        string sha256,
        EdiFormat format,
        EdiSchemaVersion schemaVersion,
        string? fileTypeCode = null)
    {
        EdiFileJob job = new()
        {
            Id = id,
            PartnerCode = partnerCode,
            FileTypeCode = fileTypeCode,
            FileName = fileName,
            SourcePath = sourcePath,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Format = format,
            SchemaVersion = schemaVersion,
            ReceivedAtUtc = DateTime.UtcNow,
            Status = EdiFileJobStatus.Received
        };

        job.AddEvent(new EdiFileReceived(job.Id, job.PartnerCode, job.FileName));
        return job;
    }

    public void SetFileTypeCode(string fileTypeCode) => FileTypeCode = fileTypeCode;

    public void MarkParsing() => Status = EdiFileJobStatus.Parsing;

    public void MarkParsed(int parsedRecords)
    {
        ParsedRecords = parsedRecords;
        Status = EdiFileJobStatus.Parsed;
    }

    public void MarkApplying() => Status = EdiFileJobStatus.Applying;

    public void MarkValidating() => Status = EdiFileJobStatus.Validating;

    public void MarkValidated() => Status = EdiFileJobStatus.Validated;

    public void MarkApplied(int appliedRecords)
    {
        AppliedRecords = appliedRecords;
        AppliedAtUtc = DateTime.UtcNow;
        Status = EdiFileJobStatus.Applied;

        AddEvent(new EdiFileApplied(Id, PartnerCode, FileName, appliedRecords));
    }

    public void Fail(string errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Status = EdiFileJobStatus.Failed;

        AddEvent(new EdiFileFailed(Id, PartnerCode, errorCode));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void AddEvent(IDomainEvent @event) => _domainEvents.Add(@event);
}
