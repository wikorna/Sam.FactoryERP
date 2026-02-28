namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Defines the metadata for a filterable field on a list report.
/// Used for server-driven UI generation of Filter Bars.
/// </summary>
public sealed record FilterFieldDef(
    string FieldName,
    string Label,
    string Type, // "string", "number", "date", "boolean", "enum", "valuehelp"
    string[] AllowedOperators, // "eq", "contains", "in", etc.
    string? ValueHelpEndpoint = null, // Endpoint if Type == "valuehelp"
    IReadOnlyCollection<StatusBadgeDef>? EnumOptions = null, // Options if Type == "enum"
    bool IsDefaultFilters = false // True if this should appear initially in the filter bar
);

/// <summary>
/// Metadata response for a List Report, instructing the UI on how to build filters and sort headers.
/// </summary>
public sealed record ListMetadataResponse(
    IReadOnlyCollection<FilterFieldDef> Filters,
    IReadOnlyCollection<string> SortableFields,
    string DefaultSort
);
