using FactoryERP.Abstractions.Caching;
using FactoryERP.Abstractions.Cqrs;
using Inventory.Application.Caching;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Commands;

/// <summary>Handler for creating an item master record.</summary>
public sealed class CreateItemCommandHandler(IInventoryDbContext db, ICacheService cache)
    : IRequestHandler<CreateItemCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateItemCommand request, CancellationToken cancellationToken)
    {
        // Business rule: item number must be unique
        var normalizedNumber = request.ItemNumber.Trim().ToUpperInvariant();
        var exists = await db.Items.AnyAsync(
            i => i.ItemNumber == normalizedNumber, cancellationToken);

        if (exists)
            return Result.Failure<Guid>(AppError.Conflict($"Item '{request.ItemNumber}' already exists."));

        var item = Item.Create(
            request.ItemNumber,
            request.Description,
            request.BaseUom,
            request.MaterialGroup,
            request.LongDescription);

        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        // Invalidate all item list/value-help caches (new item affects paginated results)
        await cache.InvalidateTagAsync(InventoryCacheKeys.TagItems, cancellationToken);

        return item.Id;
    }
}
