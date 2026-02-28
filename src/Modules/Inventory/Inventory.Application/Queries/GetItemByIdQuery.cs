using FactoryERP.Abstractions.Cqrs;
using Inventory.Application.Dtos;

namespace Inventory.Application.Queries;

/// <summary>Fiori Object Page: get full item detail by ID.</summary>
public sealed record GetItemByIdQuery(Guid Id) : IQuery<ItemDetailDto>;
