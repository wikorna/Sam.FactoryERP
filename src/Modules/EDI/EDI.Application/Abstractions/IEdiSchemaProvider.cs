using EDI.Domain.Enums;

namespace EDI.Application.Abstractions;

/// <summary>
/// Schema descriptor for an EDI file type, loaded from configuration (JSON or DB).
/// </summary>
public sealed record EdiSchema(
    string              SchemaKey,
    string              SchemaVersion,
    EdiFileType         FileType,
    IReadOnlyList<string> RequiredHeaders,
    IReadOnlyList<string> OptionalHeaders,
    IReadOnlyDictionary<string, string> HeaderAliases,
    bool                HasSegmentMarkers = false,
    string?             HeaderRowMarker   = null,
    int                 SegmentMarkerColumn = 0,
    IReadOnlyList<string>? MetadataRowMarkers = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? MetadataFields = null,
    string?             DisplayName = null,
    int                 SkipLines = 0);

/// <summary>
/// Provides EDI file schemas keyed by <see cref="EdiFileType"/>.
/// </summary>
public interface IEdiSchemaProvider
{
    /// <summary>Returns the schema for the given file type, or null if not configured.</summary>
    Task<EdiSchema?> GetSchemaAsync(EdiFileType fileType, CancellationToken ct);
}

