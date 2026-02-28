namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Standard paged response matching Fiori List Report data shape.
/// Returned by all list query handlers.
/// </summary>
public sealed record PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;
}
