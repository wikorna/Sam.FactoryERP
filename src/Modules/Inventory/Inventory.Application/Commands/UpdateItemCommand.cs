using FactoryERP.Abstractions.Cqrs;

namespace Inventory.Application.Commands;

/// <summary>Updates an existing item master (with RowVersion for optimistic concurrency).</summary>
public sealed record UpdateItemCommand(
    Guid Id,
    string Description,
    string BaseUom,
    string? MaterialGroup,
    string? LongDescription,
    decimal? GrossWeight,
    decimal? NetWeight,
    string? WeightUnit,
    byte[] RowVersion) : ICommand;
