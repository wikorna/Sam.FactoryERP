using System.Linq.Expressions;
using System.Reflection;

namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Builds EF Core IQueryable expressions from <see cref="FilterDescriptor"/> and
/// <see cref="SortDescriptor"/> with column-allowlist protection to prevent SQL injection.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies multi-sort to a queryable using an allowlist of sortable fields.
    /// Fields not in the allowlist are silently ignored.
    /// </summary>
    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        IReadOnlyList<SortDescriptor> sorts,
        HashSet<string> allowedFields,
        string defaultSort = "Id")
    {
        IOrderedQueryable<T>? ordered = null;

        foreach (var sort in sorts)
        {
            if (!allowedFields.Contains(sort.Field))
                continue;

            var property = typeof(T).GetProperty(sort.Field,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property is null) continue;

            var param = Expression.Parameter(typeof(T));
            var body = Expression.Convert(Expression.Property(param, property), typeof(object));
            var lambda = Expression.Lambda<Func<T, object>>(body, param);

            if (ordered is null)
                ordered = sort.Direction == SortDirection.Asc
                    ? query.OrderBy(lambda)
                    : query.OrderByDescending(lambda);
            else
                ordered = sort.Direction == SortDirection.Asc
                    ? ordered.ThenBy(lambda)
                    : ordered.ThenByDescending(lambda);
        }

        if (ordered is null)
        {
            // Default sort
            var prop = typeof(T).GetProperty(defaultSort,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null)
            {
                var param = Expression.Parameter(typeof(T));
                var body = Expression.Convert(Expression.Property(param, prop), typeof(object));
                var lambda = Expression.Lambda<Func<T, object>>(body, param);
                ordered = query.OrderBy(lambda);
            }
        }

        return ordered ?? query;
    }

    /// <summary>Applies page/pageSize to a queryable.</summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 200);
        return query.Skip((safePage - 1) * safeSize).Take(safeSize);
    }
}
