using FactoryERP.Abstractions.Cqrs;

namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Standard paged list query base. All Fiori List Report / Worklist queries inherit from this.
/// Supports filtering, multi-sort, search term (q=), and pagination.
/// </summary>
public abstract record PagedQuery<T> : IQuery<PagedResponse<T>>
{
    /// <summary>1-based page number. Default: 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page (1–200). Default: 20.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Free-text search term (searches across configured columns).</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Field-level filters (from Fiori FilterBar).</summary>
    public IReadOnlyList<FilterDescriptor> Filters { get; init; } = [];

    /// <summary>Multi-sort expressions (first = primary sort).</summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = [];
}
