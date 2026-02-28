using FactoryERP.Abstractions.Cqrs;

namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Base class for Fiori Value Help (F4) queries.
/// Supports search, pagination, and dependent parameters.
/// </summary>
public abstract record ValueHelpQuery : IQuery<ValueHelpResponse>
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 30;

    /// <summary>Context parameters for dependent value helps (e.g. Plant=1000 to filter Storage Locations).</summary>
    public Dictionary<string, string> Parameters { get; init; } = [];
}

/// <summary>Value Help response with paged items.</summary>
public sealed record ValueHelpResponse
{
    public required IReadOnlyList<ValueHelpItem> Items { get; init; }
    public required int TotalCount { get; init; }
}

/// <summary>A single Value Help row: id + display text + optional extra columns.</summary>
public sealed record ValueHelpItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public IReadOnlyDictionary<string, string>? AdditionalColumns { get; init; }
}
