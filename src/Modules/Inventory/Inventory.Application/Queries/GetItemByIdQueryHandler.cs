using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using Inventory.Application.Caching;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Queries;

/// <summary>Handler for Fiori Object Page — returns header + child sections.</summary>
public sealed class GetItemByIdQueryHandler(IInventoryDbContext db, ICacheService cache)
    : IRequestHandler<GetItemByIdQuery, Result<ItemDetailDto>>
{
    public async Task<Result<ItemDetailDto>> Handle(
        GetItemByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = InventoryCacheKeys.ItemById(request.Id);
        var settings = InventoryCacheKeys.MasterData(
            InventoryCacheKeys.TagItems,
            InventoryCacheKeys.TagItem(request.Id));

        // Cache the DTO projection. HybridCache is stampede-proof (single-flight per key).
        // On cache miss, the factory executes once; on cache failure, factory re-executes (fail-open).
        var dto = await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var item = await db.Items
                    .AsNoTracking()
                    .Include(i => i.Uoms)
                    .Include(i => i.Locations)
                    .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

                if (item is null)
                    return null!; // Will be wrapped in Result.Failure below

                return new ItemDetailDto
                {
                    Id = item.Id,
                    ItemNumber = item.ItemNumber,
                    Description = item.Description,
                    LongDescription = item.LongDescription,
                    BaseUom = item.BaseUom,
                    MaterialGroup = item.MaterialGroup,
                    Status = item.Status,
                    GrossWeight = item.GrossWeight,
                    NetWeight = item.NetWeight,
                    WeightUnit = item.WeightUnit,
                    RowVersion = item.RowVersion,
                    CreatedAtUtc = item.CreatedAtUtc,
                    CreatedBy = item.CreatedBy,
                    ModifiedAtUtc = item.ModifiedAtUtc,
                    ModifiedBy = item.ModifiedBy,
                    Uoms = item.Uoms.Select(u => new ItemUomDto(u.Id, u.UomCode, u.ConversionFactor)).ToList(),
                    Locations = item.Locations.Select(l => new ItemLocationDto(l.Id, l.Plant, l.StorageLocation, l.IsDefault)).ToList()
                };
            },
            settings,
            cancellationToken);

        return dto is null
            ? Result.Failure<ItemDetailDto>(AppError.NotFound("Item", request.Id))
            : Result.Success(dto);
    }
}
