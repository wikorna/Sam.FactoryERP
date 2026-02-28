using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using FactoryERP.Abstractions.Pagination;
using Inventory.Application.Caching;
using Inventory.Domain.Enums;
using MediatR;

namespace Inventory.Application.Queries;

/// <summary>Returns standard filter metadata and status badge definitions for the UI.</summary>
public sealed record GetItemsListMetadataQuery : IQuery<ListMetadataResponse>;

public sealed class GetItemsListMetadataQueryHandler(ICacheService cache)
    : IRequestHandler<GetItemsListMetadataQuery, Result<ListMetadataResponse>>
{
    public async Task<Result<ListMetadataResponse>> Handle(
        GetItemsListMetadataQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = InventoryCacheKeys.ItemsMetadata();
        var settings = InventoryCacheKeys.StaticMetadata(InventoryCacheKeys.TagMetadata);

        var response = await cache.GetOrCreateAsync(
            cacheKey,
            _ =>
            {
                var result = BuildMetadata();
                return new ValueTask<ListMetadataResponse>(result);
            },
            settings,
            cancellationToken);

        return response;
    }

    private static ListMetadataResponse BuildMetadata()
    {
        var itemStatuses = Enum.GetValues<ItemStatus>()
            .Select(s => new StatusBadgeDef(
                Id: s.ToString(),
                Text: s.ToString(),
                State: GetBadgeState(s)))
            .ToList();

        var filters = new List<FilterFieldDef>
        {
            new FilterFieldDef(
                FieldName: "Status",
                Label: "Item Status",
                Type: "enum",
                AllowedOperators: ["eq", "in"],
                EnumOptions: itemStatuses,
                IsDefaultFilters: true),

            new FilterFieldDef(
                FieldName: "ItemNumber",
                Label: "Item Number",
                Type: "valuehelp",
                AllowedOperators: ["eq", "contains"],
                ValueHelpEndpoint: "/api/v1/inventory/items/value-help",
                IsDefaultFilters: true),

            new FilterFieldDef(
                FieldName: "MaterialGroup",
                Label: "Material Group",
                Type: "string",
                AllowedOperators: ["eq", "contains"],
                IsDefaultFilters: false)
        };

        var sortFields = new[]
        {
            "ItemNumber", "Description", "MaterialGroup", "BaseUom",
            "Status", "CreatedAtUtc", "ModifiedAtUtc"
        };

        return new ListMetadataResponse(
            Filters: filters,
            SortableFields: sortFields,
            DefaultSort: "ItemNumber");
    }

    private static UiBadgeState GetBadgeState(ItemStatus status) => status switch
    {
        ItemStatus.Active => UiBadgeState.Success,
        ItemStatus.Blocked => UiBadgeState.Warning,
        ItemStatus.Inactive => UiBadgeState.Error,
        _ => UiBadgeState.None
    };
}
