using EDI.Domain.ValueObjects;

namespace EDI.Domain.Entities;

public sealed class PartnerProfile
{
    public string PartnerCode { get; }
    public string DisplayName { get; }

    public EdiFormat Format { get; }
    public EdiSchemaVersion SchemaVersion { get; }

    // Optional: routing / folder policy
    public string InboxPath { get; }
    public string ProcessingPath { get; }
    public string ArchivePath { get; }
    public string ErrorPath { get; }

    public PartnerProfile(
        string partnerCode,
        string displayName,
        EdiFormat format,
        EdiSchemaVersion schemaVersion,
        string inboxPath,
        string processingPath,
        string archivePath,
        string errorPath)
    {
        PartnerCode = partnerCode;
        DisplayName = displayName;
        Format = format;
        SchemaVersion = schemaVersion;
        InboxPath = inboxPath;
        ProcessingPath = processingPath;
        ArchivePath = archivePath;
        ErrorPath = errorPath;
    }
}
