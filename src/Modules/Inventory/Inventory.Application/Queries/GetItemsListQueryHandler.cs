using FactoryERP.Abstractions.Cqrs;
using FactoryERP.Abstractions.Pagination;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Queries;

/// <summary>Handler for Fiori List Report — filters, sorts, pages, and projects to DTOs.</summary>
public sealed class GetItemsListQueryHandler(IInventoryDbContext db)
    : IRequestHandler<GetItemsListQuery, Result<PagedResponse<ItemListDto>>>
{
    private static readonly HashSet<string> SortableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ItemNumber", "Description", "MaterialGroup", "BaseUom",
        "Status", "CreatedAtUtc", "ModifiedAtUtc"
    };

    public async Task<Result<PagedResponse<ItemListDto>>> Handle(
        GetItemsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Items.AsNoTracking().AsQueryable();

        // ── Free-text search ──
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var pattern = $"%{request.SearchTerm.Trim()}%";
            query = query.Where(i =>
                EF.Functions.Like(i.ItemNumber, pattern) ||
                EF.Functions.Like(i.Description, pattern) ||
                (i.MaterialGroup != null && EF.Functions.Like(i.MaterialGroup, pattern)));
        }

        // ── Field filters (allowlist-protected) ──
        foreach (var filter in request.Filters)
        {
            query = ApplyFilter(query, filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // ── Sorting ──
        query = query.ApplySorting(request.Sorts, SortableFields, "ItemNumber");

        // ── Paging + Projection ──
        var items = await query
            .ApplyPaging(request.Page, request.PageSize)
            .Select(i => new ItemListDto
            {
                Id = i.Id,
                ItemNumber = i.ItemNumber,
                Description = i.Description,
                BaseUom = i.BaseUom,
                MaterialGroup = i.MaterialGroup,
                Status = i.Status,
                CreatedAtUtc = i.CreatedAtUtc,
                ModifiedAtUtc = i.ModifiedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<ItemListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static IQueryable<Inventory.Domain.Entities.Item> ApplyFilter(
        IQueryable<Inventory.Domain.Entities.Item> query, FilterDescriptor filter)
    {
        return filter.Field.ToUpperInvariant() switch
        {
            "STATUS" => filter.Operator switch
            {
                FilterOperator.Eq => query.Where(i => i.Status.ToString() == filter.Value),
                _ => query
            },
            "MATERIALGROUP" => filter.Operator switch
            {
                FilterOperator.Eq => query.Where(i => i.MaterialGroup == filter.Value),
                FilterOperator.Contains => query.Where(i => i.MaterialGroup != null && i.MaterialGroup.Contains(filter.Value)),
                _ => query
            },
            "ITEMNUMBER" => filter.Operator switch
            {
                FilterOperator.Eq => query.Where(i => i.ItemNumber == filter.Value),
                FilterOperator.Contains => query.Where(i => i.ItemNumber.Contains(filter.Value)),
                FilterOperator.StartsWith => query.Where(i => i.ItemNumber.StartsWith(filter.Value)),
                _ => query
            },
            _ => query // Unknown fields silently ignored (security)
        };
    }
}
