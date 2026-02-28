using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using Inventory.Application.Caching;
using Inventory.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Commands;

/// <summary>Handler for soft-deleting an item.</summary>
public sealed class DeactivateItemCommandHandler(IInventoryDbContext db, ICacheService cache)
    : IRequestHandler<DeactivateItemCommand, Result>
{
    public async Task<Result> Handle(DeactivateItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (item is null)
            return Result.Failure(AppError.NotFound("Item", request.Id));

        item.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        // Invalidate the specific item detail cache + all list/value-help caches
        await cache.RemoveAsync(InventoryCacheKeys.ItemById(request.Id), cancellationToken);
        await cache.InvalidateTagAsync(InventoryCacheKeys.TagItems, cancellationToken);

        return Result.Success();
    }
}
