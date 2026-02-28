using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using Inventory.Application.Caching;
using Inventory.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Commands;

/// <summary>Handler for updating an item master with optimistic concurrency.</summary>
public sealed class UpdateItemCommandHandler(IInventoryDbContext db, ICacheService cache)
    : IRequestHandler<UpdateItemCommand, Result>
{
    public async Task<Result> Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (item is null)
            return Result.Failure(AppError.NotFound("Item", request.Id));

        // Optimistic concurrency check
        if (!item.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure(AppError.Conflict("The item has been modified by another user. Please refresh and try again."));

        item.Update(
            request.Description,
            request.BaseUom,
            request.MaterialGroup,
            request.LongDescription,
            request.GrossWeight,
            request.NetWeight,
            request.WeightUnit);

        await db.SaveChangesAsync(cancellationToken);

        // Invalidate the specific item detail cache + all list/value-help caches
        await cache.RemoveAsync(InventoryCacheKeys.ItemById(request.Id), cancellationToken);
        await cache.InvalidateTagAsync(InventoryCacheKeys.TagItems, cancellationToken);

        return Result.Success();
    }
}
