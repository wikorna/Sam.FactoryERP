using FactoryERP.Abstractions.Cqrs;
using MasterData.Application.Dtos;
using MasterData.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application.Queries;

/// <summary>Handler for <see cref="GetWarehousesQuery"/>.</summary>
public sealed class GetWarehousesQueryHandler(IMasterDataDbContext db)
    : IRequestHandler<GetWarehousesQuery, Result<IReadOnlyList<WarehouseListDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<WarehouseListDto>>> Handle(
        GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Warehouses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Plant))
            query = query.Where(w => w.Plant == request.Plant.Trim().ToUpperInvariant());

        if (request.IsActive.HasValue)
            query = query.Where(w => w.IsActive == request.IsActive.Value);

        var warehouses = await query
            .OrderBy(w => w.Plant)
            .ThenBy(w => w.Code)
            .Select(w => new WarehouseListDto(w.Id, w.Code, w.Name, w.Plant, w.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<WarehouseListDto>>(warehouses);
    }
}
