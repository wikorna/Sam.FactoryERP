using System.Text.Json.Serialization;
using EDI.Application.Abstractions;
using EDI.Domain.Enums;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Shared deserialization DTO for EDI schema JSON files.
/// Used by both <see cref="JsonEdiSchemaProvider"/> and <see cref="EdiSchemaRegistry"/>
/// to avoid duplicating the mapping logic.
/// </summary>
internal sealed class EdiSchemaDto
{
    public string SchemaKey { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "v1";
    public string? DisplayName { get; init; }

    [JsonPropertyName("requiredHeaders")]
    public List<string> RequiredHeaders { get; init; } = [];

    [JsonPropertyName("optionalHeaders")]
    public List<string> OptionalHeaders { get; init; } = [];

    [JsonPropertyName("headerAliases")]
    public Dictionary<string, string> HeaderAliases { get; init; } = [];

    [JsonPropertyName("hasSegmentMarkers")]
    public bool HasSegmentMarkers { get; init; }

    [JsonPropertyName("headerRowMarker")]
    public string? HeaderRowMarker { get; init; }

    [JsonPropertyName("segmentMarkerColumn")]
    public int SegmentMarkerColumn { get; init; }

    [JsonPropertyName("metadataRowMarkers")]
    public List<string>? MetadataRowMarkers { get; init; }

    [JsonPropertyName("metadataFields")]
    public Dictionary<string, List<string>>? MetadataFields { get; init; }

    [JsonPropertyName("skipLines")]
    public int SkipLines { get; init; }

    public EdiSchema ToSchema(EdiFileType fileType)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>>? metaFields = null;
        if (MetadataFields is { Count: > 0 })
        {
            metaFields = MetadataFields.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
        }

        return new EdiSchema(
            SchemaKey:           SchemaKey,
            SchemaVersion:       SchemaVersion,
            FileType:            fileType,
            RequiredHeaders:     RequiredHeaders.AsReadOnly(),
            OptionalHeaders:     OptionalHeaders.AsReadOnly(),
            HeaderAliases:       HeaderAliases,
            HasSegmentMarkers:   HasSegmentMarkers,
            HeaderRowMarker:     HeaderRowMarker,
            SegmentMarkerColumn: SegmentMarkerColumn,
            MetadataRowMarkers:  MetadataRowMarkers?.AsReadOnly(),
            MetadataFields:      metaFields,
            DisplayName:         DisplayName,
            SkipLines:           SkipLines);
    }

    /// <summary>
    /// Validates schema invariants at load time. Returns a list of issues (empty = valid).
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(SchemaKey))
            issues.Add("SchemaKey is required.");

        if (RequiredHeaders.Count == 0)
            issues.Add("At least one required header must be defined.");

        if (HasSegmentMarkers && string.IsNullOrWhiteSpace(HeaderRowMarker))
            issues.Add("HeaderRowMarker is required when hasSegmentMarkers is true.");

        if (SkipLines < 0)
            issues.Add("SkipLines must be >= 0.");

        if (HasSegmentMarkers && SkipLines > 0)
            issues.Add("SkipLines should be 0 when using segment markers (markers determine header location).");

        // Validate metadata marker references
        if (MetadataFields is { Count: > 0 } && MetadataRowMarkers is { Count: > 0 })
        {
            var markerSet = new HashSet<string>(MetadataRowMarkers, StringComparer.OrdinalIgnoreCase);
            foreach (var key in MetadataFields.Keys)
            {
                // For segment-marker schemas, metadata keys should match declared markers
                if (HasSegmentMarkers && !markerSet.Contains(key))
                    issues.Add($"Metadata field key '{key}' is not in metadataRowMarkers list.");
            }
        }

        // Check for duplicate required headers
        var duplicates = RequiredHeaders
            .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            issues.Add($"Duplicate required headers: {string.Join(", ", duplicates)}.");

        return issues;
    }
}
