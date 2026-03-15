using FactoryERP.Abstractions.Cqrs;
using MasterData.Application.Dtos;
using MediatR;

namespace MasterData.Application.Queries;

/// <summary>Returns all warehouses, optionally filtered by active status or plant.</summary>
public sealed record GetWarehousesQuery(
    string? Plant = null,
    bool? IsActive = null) : IRequest<Result<IReadOnlyList<WarehouseListDto>>>;
