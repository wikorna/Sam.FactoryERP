using FactoryERP.Abstractions.Pagination;
using Inventory.Application.Dtos;

namespace Inventory.Application.Queries;

/// <summary>
/// Fiori List Report query: paginated, filterable, sortable item list.
/// </summary>
public sealed record GetItemsListQuery : PagedQuery<ItemListDto>;
