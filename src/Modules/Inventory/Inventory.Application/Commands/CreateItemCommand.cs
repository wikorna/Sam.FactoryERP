using FactoryERP.Abstractions.Cqrs;

namespace Inventory.Application.Commands;

/// <summary>Creates a new item master record.</summary>
public sealed record CreateItemCommand(
    string ItemNumber,
    string Description,
    string BaseUom,
    string? MaterialGroup = null,
    string? LongDescription = null) : ICommand<Guid>;
